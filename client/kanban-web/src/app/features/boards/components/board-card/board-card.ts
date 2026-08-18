import { afterRenderEffect, Component, computed, debounced, inject, input, signal, viewChild } from '@angular/core';
import { Avatar } from '../../../../shared/components/avatar/avatar';
import { AssignCardRequest, BoardMemberResponse, CardResponse } from '../../models/board.models';
import { CdkDrag } from '@angular/cdk/drag-drop';
import { rxResource } from '@angular/core/rxjs-interop';
import { BoardService } from '../../services/board.service';
import { MemberService } from '../../services/member.service';
import { OverlayModule } from '@angular/cdk/overlay';
import { Combobox, ComboboxPopup, ComboboxWidget } from '@angular/aria/combobox';
import { Listbox, Option } from '@angular/aria/listbox';
import { Dialog } from '@angular/cdk/dialog';
import { ConfirmModal } from '../../../../shared/components/confirm-modal/confirm-modal';
import { HttpErrorResponse } from '@angular/common/http';
import { extractErrorMessage } from '../../../../shared/utils/extract-error-message';
import { ErrorMessage } from '../../../../shared/components/error-message/error-message';

@Component({
  selector: 'app-board-card',
  templateUrl: './board-card.html',
  styleUrl: './board-card.css',
  imports: [Avatar, CdkDrag, Combobox, ComboboxPopup, ComboboxWidget, Listbox, Option, OverlayModule, ErrorMessage],
})
export class BoardCard {
  private boardService = inject(BoardService);
  private memberService = inject(MemberService);
  private dialog = inject(Dialog);
  card = input.required<CardResponse>();

  readonly listbox = viewChild(Listbox);
  readonly combobox = viewChild(Combobox);

  protected isDeleting = computed(() => this.boardService.deletingCardIds().has(this.card().id));
  protected isAssigning = computed(() => this.boardService.assigningCardIds().has(this.card().id));
  protected isOffline = computed(() => this.boardService.isOffline());
  protected errorMessage = signal<string | null>(null);
  popupExpanded = signal(false);
  query = signal('');
  debouncedQuery = debounced(this.query, 300);
  selectedOption = signal<BoardMemberResponse[]>([]);

  memberResource = rxResource({
    params: () => {
      const query = this.debouncedQuery.value().trim();
      return query.length >= 2 ? { query } : undefined;
    },
    stream: ({ params }) => this.memberService.search(this.boardService.boardId()!, params.query),
  });

  constructor() {
    afterRenderEffect(() => {
      if (this.combobox()?.expanded() === true) {
        this.listbox()?.scrollActiveItemIntoView();
      }
    });
  }

  onBlur() {
    this.popupExpanded.set(false);
  }

  onCommit() {
    const [selected] = this.selectedOption();
    if (!selected) {
      this.popupExpanded.set(false);
      return;
    }

    this.errorMessage.set(null);

    const request: AssignCardRequest = { userId: selected.memberId };
    this.boardService.assignCard(this.card().id, request).subscribe({
      next: () => this.clear(),
      error: (err: HttpErrorResponse) => {
        this.errorMessage.set(extractErrorMessage(err, 'Failed to assign member to card.'));
      },
    });
  }

  onDeleteClick() {
    const dialogRef = this.dialog.open<boolean>(ConfirmModal);
    dialogRef.closed.subscribe((isConfirmed) => {
      if (isConfirmed) {
        this.boardService.deleteCard(this.card().id).subscribe();
      }
    });
  }

  clear() {
    this.query.set('');
    this.selectedOption.set([]);
    this.popupExpanded.set(false);
    this.errorMessage.set(null);
  }
}
