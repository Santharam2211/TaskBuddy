import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FriendRequest, User } from '../core/models/models';
import { FriendService } from '../core/services/friend.service';

@Component({
  selector: 'app-friends',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './friends.component.html',
  styleUrls: ['./friends.component.css']
})
export class FriendsComponent implements OnInit {
  friends = signal<User[]>([]);
  pendingReceived = signal<FriendRequest[]>([]);
  pendingSent = signal<FriendRequest[]>([]);
  searchResults = signal<User[]>([]);

  searchEmail = '';
  searching = signal(false);
  loading = signal(true);
  actionMessage = signal<{ text: string; isError: boolean } | null>(null);

  activeTab = signal<'friends' | 'requests' | 'find'>('friends');

  constructor(private friendService: FriendService) {}

  ngOnInit(): void {
    this.loadAllFriendsData();
  }

  loadAllFriendsData(): void {
    this.loading.set(true);
    this.friendService.getFriends().subscribe({
      next: (f) => this.friends.set(f),
      error: () => {}
    });

    this.friendService.getFriendRequests().subscribe({
      next: (res) => {
        this.pendingReceived.set(res.received);
        this.pendingSent.set(res.sent);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  searchUsers(): void {
    if (!this.searchEmail.trim()) {
      this.searchResults.set([]);
      return;
    }

    this.searching.set(true);
    this.friendService.searchFriends(this.searchEmail.trim()).subscribe({
      next: (results) => {
        this.searchResults.set(results);
        this.searching.set(false);
      },
      error: () => this.searching.set(false)
    });
  }

  sendRequest(email: string): void {
    this.friendService.sendFriendRequest(email).subscribe({
      next: (res) => {
        this.showMessage(res.message, false);
        this.searchUsers(); // refresh status
        this.loadAllFriendsData();
      },
      error: (err) => {
        this.showMessage(err.error?.message || 'Failed to send request.', true);
      }
    });
  }

  acceptRequest(requestId: number): void {
    this.friendService.acceptFriendRequest(requestId).subscribe({
      next: (res) => {
        this.showMessage(res.message, false);
        this.loadAllFriendsData();
      },
      error: (err) => {
        this.showMessage(err.error?.message || 'Failed to accept request.', true);
      }
    });
  }

  rejectRequest(requestId: number): void {
    this.friendService.rejectFriendRequest(requestId).subscribe({
      next: (res) => {
        this.showMessage(res.message, false);
        this.loadAllFriendsData();
      },
      error: (err) => {
        this.showMessage(err.error?.message || 'Failed to reject request.', true);
      }
    });
  }

  removeFriend(friendId: number, friendName: string): void {
    if (confirm(`Are you sure you want to remove ${friendName} from your friends?`)) {
      this.friendService.removeFriend(friendId).subscribe({
        next: (res) => {
          this.showMessage(res.message, false);
          this.loadAllFriendsData();
        },
        error: (err) => {
          this.showMessage(err.error?.message || 'Failed to remove friend.', true);
        }
      });
    }
  }

  private showMessage(text: string, isError: boolean): void {
    this.actionMessage.set({ text, isError });
    setTimeout(() => this.actionMessage.set(null), 4000);
  }
}
