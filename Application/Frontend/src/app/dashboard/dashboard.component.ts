import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DashboardRecent, DashboardSummary, Schedule } from '../core/models/models';
import { DashboardService } from '../core/services/dashboard.service';
import { ScheduleService } from '../core/services/schedule.service';
import { AuthService } from '../core/services/auth.service';
import { TaskModalComponent } from '../shared/components/task-modal.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, TaskModalComponent],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  summary = signal<DashboardSummary | null>(null);
  upcomingTasks = signal<Schedule[]>([]);
  recent = signal<DashboardRecent | null>(null);
  loading = signal(true);
  isTaskModalOpen = signal(false);

  constructor(
    private dashboardService: DashboardService,
    private scheduleService: ScheduleService,
    public authService: AuthService
  ) {}

  ngOnInit(): void {
    this.loadDashboardData();
  }

  loadDashboardData(): void {
    this.loading.set(true);
    this.dashboardService.getSummary().subscribe({
      next: (sum) => this.summary.set(sum),
      error: () => {}
    });

    this.dashboardService.getUpcoming(6).subscribe({
      next: (upc) => this.upcomingTasks.set(upc),
      error: () => {}
    });

    this.dashboardService.getRecent().subscribe({
      next: (rec) => {
        this.recent.set(rec);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  openCreateTaskModal(): void {
    this.isTaskModalOpen.set(true);
  }

  onTaskSaved(schedule: Schedule): void {
    this.loadDashboardData();
  }

  completeTask(scheduleId: number): void {
    this.scheduleService.completeSchedule(scheduleId).subscribe({
      next: () => this.loadDashboardData()
    });
  }

  getTodayDateString(): string {
    return new Date().toLocaleDateString('en-US', {
      weekday: 'long',
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    });
  }
}
