export interface BoardSummaryResponse {
  id: number;
  name: string;
  userRole: string;
}

export interface BoardDetailsResponse {
  id: number;
  name: string;
  description: string | null;
  columns: ColumnResponse[];
}

export interface ColumnResponse {
  id: number;
  title: string;
  description: string | null;
  position: number;
  cards: CardResponse[];
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

export interface MoveCardRequest {
  targetColumnId: number;
  targetPosition: number;
  expectedColumnId: number;
  expectedPosition: number;
}

export interface MoveCardResponse {
  cardId: number;
  columnId: number;
  position: number;
  affectedColumns: AffectedColumnResponse[];
}

export interface AffectedColumnResponse {
  columnId: number;
  cards: CardPositionResponse[];
}

export interface CardPositionResponse {
  cardId: number;
  position: number;
}
