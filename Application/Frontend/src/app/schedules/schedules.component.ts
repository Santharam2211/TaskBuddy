import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Schedule, User } from '../core/models/models';
import { ScheduleService } from '../core/services/schedule.service';
import { FriendService } from '../core/services/friend.service';
import { TaskModalComponent } from '../shared/components/task-modal.component';

@Component({
  selector: 'app-schedules',
  standalone: true,
  imports: [CommonModule, FormsModule, TaskModalComponent],
  templateUrl: './schedules.component.html',
  styleUrls: ['./schedules.component.css']
})
export class SchedulesComponent implements OnInit {
  schedules = signal<Schedule[]>([]);
  filteredSchedules = signal<Schedule[]>([]);
  loading = signal(true);

  // Filters
  activeTab = signal<'all' | 'personal' | 'shared' | 'overdue' | 'completed'>('all');
  searchQuery = '';
  selectedPriority = '';
  selectedStatus = '';

  // Modals
  isTaskModalOpen = signal(false);
  editSchedule = signal<Schedule | null>(null);
  selectedScheduleForDetails = signal<Schedule | null>(null);

  // Participant addition modal
  isAddParticipantModalOpen = signal(false);
  currentSharedSchedule = signal<Schedule | null>(null);
  friends = signal<User[]>([]);
  selectedFriendToAdd = signal<number | null>(null);

  constructor(
    private scheduleService: ScheduleService,
    private friendService: FriendService
  ) {}

  ngOnInit(): void {
    this.loadSchedules();
    this.loadFriends();
  }

  loadSchedules(): void {
    this.loading.set(true);
    this.scheduleService.getSchedules().subscribe({
      next: (data) => {
        this.schedules.set(data);
        this.applyFilters();
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  loadFriends(): void {
    this.friendService.getFriends().subscribe({
      next: (data) => this.friends.set(data),
      error: () => {}
    });
  }

  setTab(tab: 'all' | 'personal' | 'shared' | 'overdue' | 'completed'): void {
    this.activeTab.set(tab);
    this.applyFilters();
  }

  applyFilters(): void {
    let list = this.schedules();
    const tab = this.activeTab();

    if (tab === 'personal') {
      list = list.filter(s => s.scheduleType === 'Personal');
    } else if (tab === 'shared') {
      list = list.filter(s => s.scheduleType === 'Shared');
    } else if (tab === 'overdue') {
      const now = new Date();
      list = list.filter(s => s.status === 'Overdue' || (s.status !== 'Completed' && s.status !== 'Cancelled' && new Date(s.endTime) < now));
    } else if (tab === 'completed') {
      list = list.filter(s => s.status === 'Completed');
    }

    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase().trim();
      list = list.filter(s => s.taskTitle.toLowerCase().includes(q) || (s.description && s.description.toLowerCase().includes(q)));
    }

    if (this.selectedPriority) {
      list = list.filter(s => s.priority === this.selectedPriority);
    }

    if (this.selectedStatus) {
      list = list.filter(s => s.status === this.selectedStatus);
    }

    this.filteredSchedules.set(list);
  }

  openCreateModal(): void {
    this.editSchedule.set(null);
    this.isTaskModalOpen.set(true);
  }

  openEditModal(schedule: Schedule): void {
    this.editSchedule.set(schedule);
    this.isTaskModalOpen.set(true);
  }

  deleteSchedule(scheduleId: number): void {
    if (confirm('Are you sure you want to delete this schedule? This action cannot be undone.')) {
      this.scheduleService.deleteSchedule(scheduleId).subscribe({
        next: () => this.loadSchedules()
      });
    }
  }

  completeSchedule(scheduleId: number): void {
    this.scheduleService.completeSchedule(scheduleId).subscribe({
      next: () => this.loadSchedules()
    });
  }

  viewDetails(schedule: Schedule): void {
    this.selectedScheduleForDetails.set(schedule);
  }

  closeDetails(): void {
    this.selectedScheduleForDetails.set(null);
  }

  openAddParticipantModal(schedule: Schedule): void {
    this.currentSharedSchedule.set(schedule);
    this.selectedFriendToAdd.set(null);
    this.isAddParticipantModalOpen.set(true);
  }

  closeAddParticipantModal(): void {
    this.isAddParticipantModalOpen.set(false);
    this.currentSharedSchedule.set(null);
  }

  confirmAddParticipant(): void {
    const friendId = this.selectedFriendToAdd();
    const sched = this.currentSharedSchedule();
    if (friendId && sched) {
      this.scheduleService.addParticipant(sched.scheduleId, friendId).subscribe({
        next: () => {
          this.closeAddParticipantModal();
          this.loadSchedules();
        }
      });
    }
  }

  getEligibleFriendsForSchedule(schedule: Schedule | null): User[] {
    if (!schedule) return [];
    const participantUserIds = schedule.participants.map(p => p.userId);
    return this.friends().filter(f => !participantUserIds.includes(f.userId) && f.userId !== schedule.createdBy);
  }
}
