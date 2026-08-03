export interface BoardSummaryResponse {
  id: number;
  name: string;
  userRole: string;
}

export interface BoardDetailsResponse {
  id: number;
  name: string;
  description: string | null;
  columns: readonly ColumnResponse[];
}

export interface ColumnResponse {
  id: number;
  title: string;
  description: string | null;
  position: number;
  cards: readonly CardResponse[];
}

export interface CardResponse {
  id: number;
  title: string;
  description: string | null;
  position: number;
  assignedTo: CardAssigneeResponse | null;
}

export interface CardAssigneeResponse {
  userId: string;
  userName: string | null;
  email: string | null;
}
