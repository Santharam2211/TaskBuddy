import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DashboardRecent, DashboardSummary, Schedule } from '../models/models';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private readonly apiUrl = 'http://localhost:5000/api/dashboard';

  constructor(private http: HttpClient) {}

  getSummary(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(`${this.apiUrl}/summary`);
  }

  getUpcoming(limit: number = 5): Observable<Schedule[]> {
    return this.http.get<Schedule[]>(`${this.apiUrl}/upcoming`, {
      params: { limit: limit.toString() }
    });
  }

  getRecent(): Observable<DashboardRecent> {
    return this.http.get<DashboardRecent>(`${this.apiUrl}/recent`);
  }
}
