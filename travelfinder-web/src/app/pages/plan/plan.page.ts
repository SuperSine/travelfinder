import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { PlanClient } from '../../core/api/plan-client';
import {
  ChatMessageDto,
  GeoPoint,
  PlanRequest,
} from '../../core/domain/models';
import { PlanningSessionStore } from '../../core/state/planning-session.store';
import { MapComponent } from '../../features/map/map.component';
import { ChatPanelComponent } from '../../features/chat/chat-panel.component';
import { ItineraryCardsComponent } from '../../features/plan/itinerary-cards.component';
import GraphicsLayer from '@arcgis/core/layers/GraphicsLayer';
import { Place as ApiPlace } from 'src/app/services/api.service';

interface PendingPlan {
  requestId: string;
  prompt: string;
  latitude: number | null;
  longitude: number | null;
}

@Component({
  selector: 'sp-plan',
  templateUrl: './plan.page.html',
  standalone: true,
  imports: [CommonModule, ChatPanelComponent, ItineraryCardsComponent, MapComponent],
})
export class PlanPage implements OnInit, OnDestroy {
  readonly store = inject(PlanningSessionStore);
  readonly state$ = this.store.state$;
  private readonly client = inject(PlanClient, { optional: true }) ?? new PlanClient();
  private storeSub?: Subscription;
  private lastClarification?: string;

  messages: ChatMessageDto[] = [];
  requestId = '';
  latitude: number | null = null;
  longitude: number | null = null;
  needsMapPick = false;
  selectedId: string | null = null;

  ngOnInit(): void {
    this.storeSub = this.store.state$.subscribe(snapshot => {
      if (
        snapshot.phase === 'clarifying' &&
        snapshot.clarification &&
        snapshot.clarification !== this.lastClarification
      ) {
        this.lastClarification = snapshot.clarification;
        this.messages = [
          ...this.messages,
          { role: 'assistant', content: snapshot.clarification },
        ];
      }
    });

    const raw = sessionStorage.getItem('tf.pending');
    if (!raw) {
      return;
    }

    const pending = JSON.parse(raw) as PendingPlan;
    this.requestId = pending.requestId;
    this.needsMapPick = pending.latitude == null || pending.longitude == null;

    if (!this.needsMapPick) {
      this.latitude = pending.latitude as number;
      this.longitude = pending.longitude as number;
    }

    this.messages = [{ role: 'user', content: pending.prompt }];

    if (!this.needsMapPick) {
      void this.runPlanning();
    }
  }

  ngOnDestroy(): void {
    this.storeSub?.unsubscribe();
    this.store.abort();
  }

  onSend(text: string): void {
    this.messages = [...this.messages, { role: 'user', content: text }];
    if (this.needsMapPick && !this.hasValidCoordinates()) {
      return;
    }
    void this.runPlanning();
  }

  onMapPick(point: GeoPoint): void {
    this.latitude = point.latitude;
    this.longitude = point.longitude;
    this.needsMapPick = false;
    void this.runPlanning();
  }

  onPlaceSelect(placeId: string): void {
    this.selectedId = placeId;
  }

  onStopSelect(placeId: string): void {
    this.selectedId = placeId;
  }

  stopIds(stops: { placeId: string }[]): string[] {
    return stops.map(stop => stop.placeId);
  }

  private hasValidCoordinates(): boolean {
    return this.latitude != null && this.longitude != null;
  }

  private async runPlanning(): Promise<void> {
    if (this.needsMapPick && !this.hasValidCoordinates()) {
      return;
    }
    const request: PlanRequest = {
      requestId: this.requestId,
      messages: this.messages,
      latitude: this.latitude as number,
      longitude: this.longitude as number,
    };
    await this.store.run(this.client, request);
  }
}

/** @deprecated Legacy plan model — retained for api.service and plan-detail until Task 18 */
export interface PlanModel {
  name: string;
  description: string;
  places: ApiPlace[];
  groupPlaces: Map<number, ApiPlace[]>;
}

/** @deprecated Legacy plan model — retained for api.service and plan-detail until Task 18 */
export class Plan {
  constructor(rawPlan: any = null) {
    this.planModel = {
      name: '',
      description: '',
      places: [],
      groupPlaces: new Map<number, ApiPlace[]>(),
    };

    if (rawPlan) {
      this.updatePlan(rawPlan);
    }
  }

  static getPlaces(obj: any): ApiPlace[] {
    let locations = [];

    if (Array.isArray(obj)) {
      locations = obj;
    } else {
      locations = obj.Locations;
    }

    const places = locations
      ?.filter((place: any) => this.isValidLocation(place))
      .map(
        (location: any): ApiPlace => ({
          name: location.Name,
          formattedAddress: location.FormattedAddress,
          primaryType: location.PrimaryType,
          location: {
            latitude: location.Latitude,
            longitude: location.Longitude,
          },
          reason: location.SuggestReason,
          number: location.Number,
          day: location.Day,
          stopTime: location.Duration,
          priceLevel: location.PriceLevel,
          travelTime: 0,
          toggleStatus: false,
          suggestLocations: [],
          hint: '',
          distance: 0,
          sequence: 0,
          showDivider: false,
        })
      );

    return places;
  }

  static isValidLocation(location: any) {
    return (
      location.Name &&
      location.FormattedAddress &&
      location.PrimaryType &&
      location.Latitude &&
      location.Longitude &&
      location.SuggestReason &&
      location.Number
    );
  }

  static toLocationJSON(place: ApiPlace) {
    return {
      Name: place.name,
      FormattedAddress: place.formattedAddress,
      PrimaryType: place.primaryType,
      Latitude: place.location.latitude,
      Longitude: place.location.longitude,
      SuggestReason: place.reason,
      Number: place.number,
      Day: place.day,
      Duration: place.stopTime,
      PriceLevel: place.priceLevel,
    };
  }

  static fromPlanObj(obj: any) {
    const newPlan = new Plan();
    newPlan.planModel = obj;
    return newPlan;
  }

  totalFormattedTime() {
    return this.formatMinutesToDHMS(this.totalTime());
  }

  totalTime() {
    return this.planModel.places.reduce(
      (total, place) => total + place.stopTime + place.travelTime,
      0
    );
  }

  totalDistance() {
    const value = this.planModel.places.reduce((total, place) => total + place.distance, 0);
    return Number(value).toFixed(2);
  }

  toLocationJSON() {
    return {
      Name: this.planModel.name,
      Description: this.planModel.description,
      Locations: this.planModel.places.map((place: any) => ({
        Name: place.name,
        FormattedAddress: place.formattedAddress,
        PrimaryType: place.primaryType,
        Latitude: place.location.latitude,
        Longitude: place.location.longitude,
        SuggestReason: place.reason,
        Number: place.number,
        Day: place.day,
        Duration: place.stopTime,
        PriceLevel: place.priceLevel,
      })),
    };
  }

  updatePlan(detlaPlan: any) {
    this.planModel.name = detlaPlan?.Name;
    this.planModel.description = detlaPlan?.Description;
    this.planModel.places = Plan.getPlaces(detlaPlan);
  }

  updatePlanByLocations(locations: []) {
    this.planModel.places = Plan.getPlaces(locations);
  }

  updateTravelTime(stopLayer: GraphicsLayer) {
    const stopInfoList = stopLayer.graphics
      .map((graphic: any) => graphic.attributes)
      .sort((a: any, b: any) => a.Sequence - b.Sequence);

    stopInfoList?.forEach((stop: any, index: number) => {
      if (index > 0) {
        stop.TravelTime = stop.Cumul_TravelTime - stopInfoList.getItemAt(index - 1).Cumul_TravelTime;
        stop.TravelDistance =
          stop.Cumul_Kilometers - stopInfoList.getItemAt(index - 1).Cumul_Kilometers;
      } else {
        stop.TravelTime = 0;
        stop.TravelDistance = 0;
      }
    });

    const places = this.getPlaces();
    places.forEach(place => {
      place.travelTime =
        stopInfoList?.find(
          (stop: any) => stop.Number == place.number && stop.Day == place.day
        )?.TravelTime || 0;
      place.distance =
        stopInfoList?.find(
          (stop: any) => stop.Number == place.number && stop.Day == place.day
        )?.TravelDistance || 0;
      place.sequence =
        stopInfoList?.find(
          (stop: any) => stop.Number == place.number && stop.Day == place.day
        )?.Sequence || 0;
    });
  }

  getPlaces() {
    return this.planModel.places || [];
  }

  formatMinutesToDHMS(minutes: number): string {
    const days = Math.floor(minutes / 1440);
    const remainingMinutesAfterDays = minutes % 1440;
    const hours = Number(Math.floor(remainingMinutesAfterDays / 60)).toFixed(2);
    const mins = Number(remainingMinutesAfterDays % 60).toFixed(2);
    return `${days.toString().padStart(2, '0')} Days, ${hours.toString().padStart(2, '0')} Hours, ${mins.toString().padStart(2, '0')} Minutes`;
  }

  planModel!: PlanModel;
}
