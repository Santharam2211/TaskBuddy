import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { FriendRequestsResponse, User } from '../models/models';

@Injectable({
  providedIn: 'root'
})
export class FriendService {
  private readonly apiUrl = 'http://localhost:5000/api/friends';

  constructor(private http: HttpClient) {}

  getFriends(): Observable<User[]> {
    return this.http.get<User[]>(this.apiUrl);
  }

  getFriendRequests(): Observable<FriendRequestsResponse> {
    return this.http.get<FriendRequestsResponse>(`${this.apiUrl}/requests`);
  }

  searchFriends(email: string): Observable<User[]> {
    return this.http.get<User[]>(`${this.apiUrl}/search`, { params: { email } });
  }

  sendFriendRequest(email: string): Observable<{ message: string; data?: any }> {
    return this.http.post<{ message: string; data?: any }>(`${this.apiUrl}/request`, { email });
  }

  acceptFriendRequest(requestId: number): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.apiUrl}/accept/${requestId}`, {});
  }

  rejectFriendRequest(requestId: number): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.apiUrl}/reject/${requestId}`, {});
  }

  removeFriend(friendId: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.apiUrl}/${friendId}`);
  }
}
