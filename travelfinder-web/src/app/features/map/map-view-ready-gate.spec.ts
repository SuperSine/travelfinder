import { MapViewReadyGate } from './map-view-ready-gate';

describe('MapViewReadyGate', () => {
  it('queues changes before ready and replays once on markReady', () => {
    const gate = new MapViewReadyGate();

    expect(gate.isReady()).toBeFalse();
    expect(gate.shouldApply()).toBeFalse();

    gate.onChange();
    expect(gate.markReady()).toBeTrue();
    expect(gate.isReady()).toBeTrue();
    expect(gate.shouldApply()).toBeTrue();
  });

  it('does not require replay when no changes occurred before ready', () => {
    const gate = new MapViewReadyGate();

    expect(gate.markReady()).toBeFalse();
    expect(gate.shouldApply()).toBeTrue();
  });

  it('applies changes immediately after ready', () => {
    const gate = new MapViewReadyGate();
    gate.markReady();

    gate.onChange();
    expect(gate.shouldApply()).toBeTrue();
    expect(gate.markReady()).toBeFalse();
  });
});
