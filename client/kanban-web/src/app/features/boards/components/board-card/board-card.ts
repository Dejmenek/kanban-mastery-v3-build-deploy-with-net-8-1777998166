import { afterRenderEffect, Component, debounced, inject, input, output, signal, viewChild } from '@angular/core';
import { Avatar } from '../../../../shared/components/avatar/avatar';
import { AssignCardRequest, BoardMemberResponse, CardResponse } from '../../models/board.models';
import { CdkDrag } from '@angular/cdk/drag-drop';
import { rxResource } from '@angular/core/rxjs-interop';
import { BoardService } from '../../services/board.service';
import { OverlayModule } from '@angular/cdk/overlay';
import { Combobox, ComboboxPopup, ComboboxWidget } from '@angular/aria/combobox';
import { Listbox, Option } from '@angular/aria/listbox';
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
  card = input.required<CardResponse>();
  boardId = input.required<number>();
  deletingCardId = input<number | null>(null);
  assigned = output<CardResponse>();
  deleted = output<number>();

  readonly listbox = viewChild(Listbox);
  readonly combobox = viewChild(Combobox);

  protected errorMessage = signal<string | null>(null);
  protected isAssigning = signal(false);
  popupExpanded = signal(false);
  query = signal('');
  debouncedQuery = debounced(this.query, 300);
  selectedOption = signal<BoardMemberResponse[]>([]);

  memberResource = rxResource({
    params: () => {
      const query = this.debouncedQuery.value().trim();
      return query.length >= 2 ? { query } : undefined;
    },
    stream: ({ params }) => this.boardService.searchMembers(this.boardId(), params.query),
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
    this.isAssigning.set(true);

    const request: AssignCardRequest = { userId: selected.memberId };
    this.boardService
      .assignMember(this.boardId(), this.card().id, request)
      .pipe(finalize(() => this.isAssigning.set(false)))
      .subscribe({
        next: (card) => {
          this.assigned.emit(card);
          this.clear();
        },
        error: (err: HttpErrorResponse) => {
        this.errorMessage.set(extractErrorMessage(err, 'Failed to assign member to card.'));
        },
      });
  }

  clear() {
    this.query.set('');
    this.selectedOption.set([]);
    this.popupExpanded.set(false);
    this.errorMessage.set(null);
  }
}
