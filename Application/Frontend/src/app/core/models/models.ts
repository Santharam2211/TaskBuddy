export interface User {
  userId: number;
  fullName: string;
  email: string;
  friendStatus?: string; // "Friend", "PendingSent", "PendingReceived", "None"
  requestId?: number;
}

export interface AuthResponse {
  token: string;
  userId: number;
  fullName: string;
  email: string;
  createdAt: string;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface FriendRequest {
  requestId: number;
  senderId: number;
  senderName: string;
  senderEmail: string;
  receiverId: number;
  receiverName: string;
  receiverEmail: string;
  status: string;
  createdAt: string;
  updatedAt: string;
}

export interface FriendRequestsResponse {
  received: FriendRequest[];
  sent: FriendRequest[];
}

export interface Participant {
  participantId: number;
  userId: number;
  fullName: string;
  email: string;
  invitationStatus: string;
  joinedAt: string;
}

export interface Schedule {
  scheduleId: number;
  createdBy: number;
  creatorName: string;
  creatorEmail: string;
  isCreator: boolean;
  taskTitle: string;
  description?: string;
  startTime: string;
  endTime: string;
  priority: 'Low' | 'Medium' | 'High';
  scheduleType: 'Personal' | 'Shared';
  status: 'Scheduled' | 'InProgress' | 'Overdue' | 'Completed' | 'Cancelled';
  completedBy?: number;
  completedByName?: string;
  completedAt?: string;
  createdAt: string;
  updatedAt: string;
  participants: Participant[];
}

export interface CreateScheduleRequest {
  taskTitle: string;
  description?: string;
  startTime: string;
  endTime: string;
  priority: string;
  scheduleType: string;
  participantUserIds: number[];
}

export interface UpdateScheduleRequest {
  taskTitle: string;
  description?: string;
  startTime: string;
  endTime: string;
  priority: string;
  status: string;
  participantUserIds?: number[];
}

export interface NotificationItem {
  notificationId: number;
  userId: number;
  scheduleId?: number;
  taskTitle?: string;
  notificationType: string;
  message: string;
  isRead: boolean;
  createdAt: string;
}

export interface DashboardSummary {
  totalTasks: number;
  upcomingTasks: number;
  completedTasks: number;
  overdueTasks: number;
  sharedTasks: number;
  pendingFriendRequests: number;
}

export interface DashboardRecent {
  recentlyCompletedTasks: Schedule[];
  recentNotifications: NotificationItem[];
}
