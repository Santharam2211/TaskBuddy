import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateScheduleRequest, Schedule, UpdateScheduleRequest } from '../models/models';

@Injectable({
  providedIn: 'root'
})
export class ScheduleService {
  private readonly apiUrl = 'http://localhost:5000/api/schedules';

  constructor(private http: HttpClient) {}

  getSchedules(filters?: {
    status?: string;
    priority?: string;
    fromDate?: string;
    toDate?: string;
    search?: string;
  }): Observable<Schedule[]> {
    let params = new HttpParams();
    if (filters) {
      if (filters.status) params = params.set('status', filters.status);
      if (filters.priority) params = params.set('priority', filters.priority);
      if (filters.fromDate) params = params.set('fromDate', filters.fromDate);
      if (filters.toDate) params = params.set('toDate', filters.toDate);
      if (filters.search) params = params.set('search', filters.search);
    }
    return this.http.get<Schedule[]>(this.apiUrl, { params });
  }

  getSharedSchedules(): Observable<Schedule[]> {
    return this.http.get<Schedule[]>(`${this.apiUrl}/shared`);
  }

  getScheduleById(id: number): Observable<Schedule> {
    return this.http.get<Schedule>(`${this.apiUrl}/${id}`);
  }

  createSchedule(dto: CreateScheduleRequest): Observable<{ message: string; data: Schedule }> {
    return this.http.post<{ message: string; data: Schedule }>(this.apiUrl, dto);
  }

  updateSchedule(id: number, dto: UpdateScheduleRequest): Observable<{ message: string; data: Schedule }> {
    return this.http.put<{ message: string; data: Schedule }>(`${this.apiUrl}/${id}`, dto);
  }

  deleteSchedule(id: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.apiUrl}/${id}`);
  }

  completeSchedule(id: number): Observable<{ message: string; data: Schedule }> {
    return this.http.put<{ message: string; data: Schedule }>(`${this.apiUrl}/${id}/complete`, {});
  }

  addParticipant(scheduleId: number, userId: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/${scheduleId}/participants`, { userId });
  }
}
