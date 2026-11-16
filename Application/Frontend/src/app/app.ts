import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterModule, Router } from '@angular/router';
import { AuthService } from './core/services/auth.service';
import { NotificationService } from './core/services/notification.service';
import { TaskModalComponent } from './shared/components/task-modal.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterModule, TaskModalComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  isSidebarOpen = signal(false);
  isQuickTaskModalOpen = signal(false);

  constructor(
    public authService: AuthService,
    public notificationService: NotificationService,
    private router: Router
  ) {}

  ngOnInit(): void {
    if (this.authService.isAuthenticated()) {
      this.notificationService.getNotifications(1).subscribe({
        error: () => {}
      });
    }
  }

  toggleSidebar(): void {
    this.isSidebarOpen.update(v => !v);
  }

  closeSidebar(): void {
    this.isSidebarOpen.set(false);
  }

  openQuickTask(): void {
    this.isQuickTaskModalOpen.set(true);
  }

  onQuickTaskSaved(): void {
    // If currently on dashboard or schedules, reload view or navigate
    if (this.router.url.includes('schedules') || this.router.url.includes('dashboard')) {
      const currentUrl = this.router.url;
      this.router.navigateByUrl('/', { skipLocationChange: true }).then(() => {
        this.router.navigate([currentUrl]);
      });
    }
  }

  logout(): void {
    this.authService.logout();
  }
}
