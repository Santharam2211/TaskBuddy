import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { NotificationItem } from '../models/models';

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private readonly apiUrl = 'http://localhost:5000/api/notifications';

  unreadCount = signal<number>(0);

  constructor(private http: HttpClient) {}

  getNotifications(limit: number = 50): Observable<{ notifications: NotificationItem[]; unreadCount: number }> {
    return this.http.get<{ notifications: NotificationItem[]; unreadCount: number }>(this.apiUrl, {
      params: { limit: limit.toString() }
    }).pipe(
      tap(res => this.unreadCount.set(res.unreadCount))
    );
  }

  markAsRead(id: number): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.apiUrl}/${id}/read`, {}).pipe(
      tap(() => this.unreadCount.update(c => Math.max(0, c - 1)))
    );
  }

  markAllAsRead(): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.apiUrl}/read-all`, {}).pipe(
      tap(() => this.unreadCount.set(0))
    );
  }
}
