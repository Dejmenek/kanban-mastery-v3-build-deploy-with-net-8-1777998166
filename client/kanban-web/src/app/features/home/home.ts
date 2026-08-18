import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/services/auth.service';

interface Feature {
  icon: string;
  title: string;
  description: string;
}

interface Step {
  number: string;
  title: string;
  description: string;
}

@Component({
  selector: 'app-home',
  templateUrl: './home.html',
  styleUrl: './home.css',
  imports: [RouterLink],
})
export class Home {
  protected auth = inject(AuthService);

  protected readonly features: Feature[] = [
    {
      icon: 'icon-columns',
      title: 'Boards & columns',
      description:
        'Lay out work exactly as your process runs. Create as many boards and columns as your team needs, from a simple To Do / Doing / Done to a multi-stage pipeline.',
    },
    {
      icon: 'icon-move',
      title: 'Drag-and-drop workflow',
      description:
        'Move cards between columns with a drag. Reorder priorities within a column just as easily. No forms, no menus, just pick up the card and put it where it belongs.',
    },
    {
      icon: 'icon-sync',
      title: 'Real-time collaboration',
      description:
        'Every card move, edit, and comment reaches teammates the moment it happens. The board on your screen is always the board everyone else is looking at.',
    },
    {
      icon: 'icon-shield',
      title: 'Role-based access',
      description:
        'Invite teammates as owners or members and control who can edit boards. Share a board without handing over the keys to your whole workspace.',
    },
  ];

  protected readonly steps: Step[] = [
    {
      number: '01',
      title: 'Create a board',
      description: 'Name it, describe it, and lay out the columns that match how your team works.',
    },
    {
      number: '02',
      title: 'Invite your team',
      description: 'Add members and assign roles so the right people can view and edit the board.',
    },
    {
      number: '03',
      title: 'Track the work',
      description: 'Add cards, drag them across columns, and watch progress update live for everyone.',
    },
  ];
}
