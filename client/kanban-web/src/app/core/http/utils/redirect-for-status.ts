export function redirectPathForStatus(status: number): string {
  switch (status) {
    case 403:
      return '/forbidden';
    case 404:
      return '/not-found';
    default:
      return '/error';
  }
}
