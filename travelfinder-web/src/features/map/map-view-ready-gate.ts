/** Tracks whether the ArcGIS view is ready and whether inputs changed before readiness. */
export class MapViewReadyGate {
  private ready = false;
  private needsReplay = false;

  isReady(): boolean {
    return this.ready;
  }

  /** Record that an input changed while the view is not yet ready. */
  onChange(): void {
    if (!this.ready) {
      this.needsReplay = true;
    }
  }

  /** Mark the view ready; returns whether queued inputs need a replay sync. */
  markReady(): boolean {
    this.ready = true;
    return this.needsReplay;
  }

  shouldApply(): boolean {
    return this.ready;
  }
}
