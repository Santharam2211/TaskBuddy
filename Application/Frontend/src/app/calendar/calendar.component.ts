import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Schedule } from '../core/models/models';
import { ScheduleService } from '../core/services/schedule.service';
import { TaskModalComponent } from '../shared/components/task-modal.component';

export interface CalendarDay {
  date: Date;
  isCurrentMonth: boolean;
  isToday: boolean;
  tasks: Schedule[];
}

@Component({
  selector: 'app-calendar',
  standalone: true,
  imports: [CommonModule, TaskModalComponent],
  templateUrl: './calendar.component.html',
  styleUrls: ['./calendar.component.css']
})
export class CalendarComponent implements OnInit {
  currentView = signal<'month' | 'week' | 'day'>('month');
  currentDate = signal<Date>(new Date());
  schedules = signal<Schedule[]>([]);
  loading = signal(false);

  // Month grid days
  monthDays = signal<CalendarDay[]>([]);
  weekDays = signal<CalendarDay[]>([]);

  // Task Details Modal
  selectedTask = signal<Schedule | null>(null);
  isTaskModalOpen = signal(false);

  constructor(private scheduleService: ScheduleService) {}

  ngOnInit(): void {
    this.loadSchedules();
  }

  loadSchedules(): void {
    this.loading.set(true);
    this.scheduleService.getSchedules().subscribe({
      next: (data) => {
        this.schedules.set(data);
        this.generateView();
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  setView(view: 'month' | 'week' | 'day'): void {
    this.currentView.set(view);
    this.generateView();
  }

  navigate(direction: number): void {
    const cur = new Date(this.currentDate());
    const view = this.currentView();

    if (view === 'month') {
      cur.setMonth(cur.getMonth() + direction);
    } else if (view === 'week') {
      cur.setDate(cur.getDate() + direction * 7);
    } else if (view === 'day') {
      cur.setDate(cur.getDate() + direction);
    }

    this.currentDate.set(cur);
    this.generateView();
  }

  goToToday(): void {
    this.currentDate.set(new Date());
    this.generateView();
  }

  private generateView(): void {
    const view = this.currentView();
    if (view === 'month') {
      this.generateMonthDays();
    } else if (view === 'week') {
      this.generateWeekDays();
    }
  }

  private generateMonthDays(): void {
    const date = this.currentDate();
    const year = date.getFullYear();
    const month = date.getMonth();

    const firstDayIndex = new Date(year, month, 1).getDay(); // 0 is Sunday
    const lastDayCurrent = new Date(year, month + 1, 0).getDate();
    const lastDayPrev = new Date(year, month, 0).getDate();

    const days: CalendarDay[] = [];
    const today = new Date();

    // Previous month padding
    for (let i = firstDayIndex - 1; i >= 0; i--) {
      const d = new Date(year, month - 1, lastDayPrev - i);
      days.push({
        date: d,
        isCurrentMonth: false,
        isToday: this.isSameDay(d, today),
        tasks: this.getTasksForDate(d)
      });
    }

    // Current month days
    for (let i = 1; i <= lastDayCurrent; i++) {
      const d = new Date(year, month, i);
      days.push({
        date: d,
        isCurrentMonth: true,
        isToday: this.isSameDay(d, today),
        tasks: this.getTasksForDate(d)
      });
    }

    // Next month padding to fill 35 or 42 cells
    const remaining = (7 - (days.length % 7)) % 7;
    for (let i = 1; i <= remaining; i++) {
      const d = new Date(year, month + 1, i);
      days.push({
        date: d,
        isCurrentMonth: false,
        isToday: this.isSameDay(d, today),
        tasks: this.getTasksForDate(d)
      });
    }

    this.monthDays.set(days);
  }

  private generateWeekDays(): void {
    const date = this.currentDate();
    const currentDayOfWeek = date.getDay();
    const startOfWeek = new Date(date);
    startOfWeek.setDate(date.getDate() - currentDayOfWeek);

    const days: CalendarDay[] = [];
    const today = new Date();

    for (let i = 0; i < 7; i++) {
      const d = new Date(startOfWeek);
      d.setDate(startOfWeek.getDate() + i);
      days.push({
        date: d,
        isCurrentMonth: d.getMonth() === date.getMonth(),
        isToday: this.isSameDay(d, today),
        tasks: this.getTasksForDate(d)
      });
    }

    this.weekDays.set(days);
  }

  getDayTasks(): Schedule[] {
    return this.getTasksForDate(this.currentDate());
  }

  private getTasksForDate(d: Date): Schedule[] {
    return this.schedules().filter(s => {
      const start = new Date(s.startTime);
      const end = new Date(s.endTime);
      return this.isSameDay(start, d) || this.isSameDay(end, d);
    });
  }

  private isSameDay(d1: Date, d2: Date): boolean {
    return (
      d1.getFullYear() === d2.getFullYear() &&
      d1.getMonth() === d2.getMonth() &&
      d1.getDate() === d2.getDate()
    );
  }

  selectTask(task: Schedule): void {
    this.selectedTask.set(task);
  }

  closeTaskDetails(): void {
    this.selectedTask.set(null);
  }

  getMonthTitle(): string {
    return this.currentDate().toLocaleDateString('en-US', { month: 'long', year: 'numeric' });
  }

  getWeekTitle(): string {
    const days = this.weekDays();
    if (!days.length) return this.getMonthTitle();
    const start = days[0].date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    const end = days[6].date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
    return `${start} - ${end}`;
  }

  getDayTitle(): string {
    return this.currentDate().toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' });
  }
}
