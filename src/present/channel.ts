export interface ShowState {
  type: 'state';
  docId: string;
  index: number;
  step: number;
  steps: number;
  ended: boolean;
  closed?: boolean;
}

export interface ShowCommand {
  type: 'cmd';
  docId: string;
  cmd: 'next' | 'prev' | 'goto' | 'black' | 'white' | 'end';
  index?: number;
}

let channel: BroadcastChannel | null | undefined;

/** Same origin channel between the audience window and the presenter view window. */
export function showChannel(): BroadcastChannel | null {
  if (channel === undefined) {
    channel = typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel('visary-show') : null;
  }
  return channel;
}
