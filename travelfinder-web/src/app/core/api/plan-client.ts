import { environment } from '../../../environments/environment';
import { PlanEvent, PlanRequest } from '../domain/models';
import { parseSseFrames } from './sse-parser';

export class PlanClient {
  async *stream(request: PlanRequest, signal?: AbortSignal): AsyncIterable<PlanEvent> {
    const response = await fetch(`${environment.API_URL}/plan/stream`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
      body: JSON.stringify(request),
      signal,
    });

    if (!response.body) {
      yield {
        type: 'error',
        error: {
          code: 'internal',
          message: 'Empty response',
          requestId: request.requestId,
          retryable: true,
        },
      };
      return;
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const { value, done } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      const parts = buffer.split('\n\n');
      buffer = parts.pop() ?? '';
      for (const frame of parts) {
        for (const event of parseSseFrames(frame + '\n\n')) {
          yield event;
        }
      }
    }

    if (buffer.trim()) {
      for (const event of parseSseFrames(buffer + '\n\n')) {
        yield event;
      }
    }
  }
}
