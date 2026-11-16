import { Component, EventEmitter, Input, Output, OnChanges, SimpleChanges, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { CreateScheduleRequest, Schedule, UpdateScheduleRequest, User } from '../../core/models/models';
import { ScheduleService } from '../../core/services/schedule.service';
import { FriendService } from '../../core/services/friend.service';

@Component({
  selector: 'app-task-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './task-modal.component.html',
  styleUrls: ['./task-modal.component.css']
})
export class TaskModalComponent implements OnChanges {
  @Input() isOpen = false;
  @Input() editSchedule: Schedule | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() saved = new EventEmitter<Schedule>();

  taskForm: FormGroup;
  friends = signal<User[]>([]);
  selectedFriendIds = signal<number[]>([]);
  loading = signal(false);
  errorMessage = signal<string | null>(null);

  constructor(
    private fb: FormBuilder,
    private scheduleService: ScheduleService,
    private friendService: FriendService
  ) {
    const now = new Date();
    const oneHourLater = new Date(now.getTime() + 60 * 60 * 1000);

    this.taskForm = this.fb.group({
      taskTitle: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(200)]],
      description: ['', [Validators.maxLength(2000)]],
      startTime: [this.toLocalIso(now), Validators.required],
      endTime: [this.toLocalIso(oneHourLater), Validators.required],
      priority: ['Medium', Validators.required],
      scheduleType: ['Personal', Validators.required]
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen'] && this.isOpen) {
      this.loadFriends();
      if (this.editSchedule) {
        this.populateForm(this.editSchedule);
      } else {
        this.resetForm();
      }
    }
  }

  private loadFriends(): void {
    this.friendService.getFriends().subscribe({
      next: (data) => this.friends.set(data),
      error: () => {}
    });
  }

  private populateForm(s: Schedule): void {
    this.taskForm.patchValue({
      taskTitle: s.taskTitle,
      description: s.description || '',
      startTime: this.toLocalIso(new Date(s.startTime)),
      endTime: this.toLocalIso(new Date(s.endTime)),
      priority: s.priority,
      scheduleType: s.scheduleType
    });
    this.selectedFriendIds.set(s.participants.map(p => p.userId));
  }

  private resetForm(): void {
    const now = new Date();
    const oneHourLater = new Date(now.getTime() + 60 * 60 * 1000);

    this.taskForm.reset({
      taskTitle: '',
      description: '',
      startTime: this.toLocalIso(now),
      endTime: this.toLocalIso(oneHourLater),
      priority: 'Medium',
      scheduleType: 'Personal'
    });
    this.selectedFriendIds.set([]);
    this.errorMessage.set(null);
  }

  toggleFriendSelection(userId: number): void {
    const current = this.selectedFriendIds();
    if (current.includes(userId)) {
      this.selectedFriendIds.set(current.filter(id => id !== userId));
    } else {
      this.selectedFriendIds.set([...current, userId]);
    }
  }

  onSubmit(): void {
    if (this.taskForm.invalid) {
      this.taskForm.markAllAsTouched();
      return;
    }

    const val = this.taskForm.value;
    const start = new Date(val.startTime);
    const end = new Date(val.endTime);

    if (end <= start) {
      this.errorMessage.set('End time must be later than start time.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    if (this.editSchedule) {
      const updateDto: UpdateScheduleRequest = {
        taskTitle: val.taskTitle,
        description: val.description,
        startTime: start.toISOString(),
        endTime: end.toISOString(),
        priority: val.priority,
        status: this.editSchedule.status,
        participantUserIds: val.scheduleType === 'Shared' ? this.selectedFriendIds() : []
      };

      this.scheduleService.updateSchedule(this.editSchedule.scheduleId, updateDto).subscribe({
        next: (res) => {
          this.loading.set(false);
          this.saved.emit(res.data);
          this.onClose();
        },
        error: (err) => {
          this.loading.set(false);
          this.errorMessage.set(err.error?.message || 'Failed to update schedule.');
        }
      });
    } else {
      const createDto: CreateScheduleRequest = {
        taskTitle: val.taskTitle,
        description: val.description,
        startTime: start.toISOString(),
        endTime: end.toISOString(),
        priority: val.priority,
        scheduleType: val.scheduleType,
        participantUserIds: val.scheduleType === 'Shared' ? this.selectedFriendIds() : []
      };

      this.scheduleService.createSchedule(createDto).subscribe({
        next: (res) => {
          this.loading.set(false);
          this.saved.emit(res.data);
          this.onClose();
        },
        error: (err) => {
          this.loading.set(false);
          this.errorMessage.set(err.error?.message || 'Failed to create schedule.');
        }
      });
    }
  }

  onClose(): void {
    this.close.emit();
  }

  private toLocalIso(date: Date): string {
    const tzOffset = date.getTimezoneOffset() * 60000;
    const localISOTime = new Date(date.getTime() - tzOffset).toISOString().slice(0, 16);
    return localISOTime;
  }
}
