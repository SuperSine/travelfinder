import { useState } from 'react';
import { Button, Stack, Text, TextArea } from '@astryxdesign/core';
import type { ChatMessageDto } from '../../core/domain/models';
import type { SessionPhase } from '../../core/state/planning-session';
import './chat-panel.css';

export interface ChatPanelProps {
  messages: ChatMessageDto[];
  phase: SessionPhase;
  clarification?: string;
  errorMessage?: string;
  onSend: (text: string) => void;
}

export function ChatPanel({
  messages,
  phase,
  clarification,
  errorMessage,
  onSend,
}: ChatPanelProps) {
  const [draft, setDraft] = useState('');

  const submit = () => {
    const text = draft.trim();
    if (!text) {
      return;
    }
    onSend(text);
    setDraft('');
  };

  return (
    <Stack gap={3} className="chat-panel">
      <Text type="label" color="secondary">
        {phase}
      </Text>

      <div className="chat-panel__messages">
        <Stack gap={2}>
          {messages.map((message, index) => (
            <Text
              key={`${message.role}-${index}`}
              className={message.role === 'user' ? 'chat-panel__user-message' : undefined}
            >
              <Text type="label" as="span">
                {message.role === 'user' ? 'You' : 'Assistant'}:
              </Text>{' '}
              {message.content}
            </Text>
          ))}
        </Stack>
      </div>

      {clarification ? (
        <Text className="chat-panel__clarification">{clarification}</Text>
      ) : null}

      {errorMessage ? (
        <Text className="chat-panel__error">{errorMessage}</Text>
      ) : null}

      <Stack direction="horizontal" gap={2} className="chat-panel__compose">
        <div className="chat-panel__textarea">
          <TextArea
            label="Message"
            isLabelHidden
            placeholder="Reply or refine your trip…"
            rows={3}
            value={draft}
            onChange={value => setDraft(value)}
          />
        </div>
        <Button
          label="Send"
          variant="primary"
          isDisabled={!draft.trim()}
          onClick={submit}
        />
      </Stack>
    </Stack>
  );
}
