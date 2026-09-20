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

    const contentType = response.headers.get('Content-Type') ?? '';
    const isEventStream = contentType.toLowerCase().includes('text/event-stream');

    if (!response.ok || !isEventStream) {
      const message = await this.readErrorMessage(response);
      yield {
        type: 'error',
        error: {
          code: 'internal',
          message,
          requestId: request.requestId,
          retryable: true,
        },
      };
      return;
    }

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

  private async readErrorMessage(response: Response): Promise<string> {
    try {
      const text = await response.text();
      return text.trim() || response.statusText || 'Request failed';
    } catch {
      return response.statusText || 'Request failed';
    }
  }
}
