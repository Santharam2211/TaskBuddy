import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationItem } from '../core/models/models';
import { NotificationService } from '../core/services/notification.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './notifications.component.html',
  styleUrls: ['./notifications.component.css']
})
export class NotificationsComponent implements OnInit {
  notifications = signal<NotificationItem[]>([]);
  unreadCount = signal(0);
  loading = signal(true);
  filterType = signal<string>('all');

  constructor(
    private notificationService: NotificationService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadNotifications();
  }

  loadNotifications(): void {
    this.loading.set(true);
    this.notificationService.getNotifications(100).subscribe({
      next: (res) => {
        this.notifications.set(res.notifications);
        this.unreadCount.set(res.unreadCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  markAsRead(item: NotificationItem): void {
    if (item.isRead) return;
    this.notificationService.markAsRead(item.notificationId).subscribe({
      next: () => {
        this.notifications.update(list =>
          list.map(n => n.notificationId === item.notificationId ? { ...n, isRead: true } : n)
        );
        this.unreadCount.update(c => Math.max(0, c - 1));
      }
    });
  }

  markAllAsRead(): void {
    this.notificationService.markAllAsRead().subscribe({
      next: () => {
        this.notifications.update(list => list.map(n => ({ ...n, isRead: true })));
        this.unreadCount.set(0);
      }
    });
  }

  getFilteredNotifications(): NotificationItem[] {
    const filter = this.filterType();
    if (filter === 'all') return this.notifications();
    if (filter === 'unread') return this.notifications().filter(n => !n.isRead);
    return this.notifications().filter(n => n.notificationType.toLowerCase() === filter.toLowerCase());
  }

  onNotificationClick(item: NotificationItem): void {
    this.markAsRead(item);
    if (item.scheduleId) {
      this.router.navigate(['/schedules']);
    } else if (item.notificationType.includes('Friend')) {
      this.router.navigate(['/friends']);
    }
  }
}
