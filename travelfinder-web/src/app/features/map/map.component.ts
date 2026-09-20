import {
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnInit,
  Output,
  SimpleChanges,
  ViewChild,
} from '@angular/core';
import WebMap from '@arcgis/core/WebMap';
import MapView from '@arcgis/core/views/MapView';
import GraphicsLayer from '@arcgis/core/layers/GraphicsLayer';
import Graphic from '@arcgis/core/Graphic';
import Point from '@arcgis/core/geometry/Point';
import Polyline from '@arcgis/core/geometry/Polyline';
import SimpleMarkerSymbol from '@arcgis/core/symbols/SimpleMarkerSymbol';
import SimpleLineSymbol from '@arcgis/core/symbols/SimpleLineSymbol';
import PopupTemplate from '@arcgis/core/PopupTemplate';
import * as webMercatorUtils from '@arcgis/core/geometry/support/webMercatorUtils';
import { GeoPoint, Place } from '../../core/domain/models';
import { mergePlaceGraphics } from './place-graphics';

@Component({
  selector: 'app-map',
  templateUrl: './map.component.html',
  styleUrls: ['./map.component.scss'],
  standalone: true,
})
export class MapComponent implements OnInit, OnChanges {
  @Input() places: Place[] = [];
  @Input() selectedId: string | null = null;
  @Input() stopOrder: string[] = [];
  @Input() needsMapPick = false;

  @Output() ready = new EventEmitter<void>();
  @Output() placeSelect = new EventEmitter<string>();
  @Output() mapPick = new EventEmitter<GeoPoint>();

  @ViewChild('mapViewNode', { static: true })
  private mapViewEl!: ElementRef<HTMLDivElement>;

  private view?: MapView;
  private readonly placesLayer = new GraphicsLayer({ id: 'places' });
  private readonly routeLayer = new GraphicsLayer({ id: 'route' });
  private drawnIds: string[] = [];
  private routeGraphic?: Graphic;

  private readonly markerSymbol = new SimpleMarkerSymbol({
    color: '#2563eb',
    size: 10,
    outline: { color: '#ffffff', width: 1 },
  });

  private readonly selectedSymbol = new SimpleMarkerSymbol({
    color: '#dc2626',
    size: 12,
    outline: { color: '#ffffff', width: 2 },
  });

  private readonly lineSymbol = new SimpleLineSymbol({
    color: '#2563eb',
    width: 3,
  });

  webmap = new WebMap({
    basemap: 'topo-vector',
    layers: [this.placesLayer, this.routeLayer],
  });

  async ngOnInit(): Promise<void> {
    this.view = new MapView({
      container: this.mapViewEl.nativeElement,
      map: this.webmap,
      zoom: 13,
      center: [103.8198, 1.3521],
    });

    await this.view.when();
    this.view.on('click', event => this.onMapClick(event));
    this.ready.emit();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!this.view) {
      return;
    }

    if (changes['places']) {
      this.syncPlaces();
    }

    if (changes['stopOrder'] || changes['places']) {
      this.syncRoute();
    }

    if (changes['selectedId']) {
      this.syncSelection();
    }
  }

  private syncPlaces(): void {
    if (this.places.length === 0) {
      this.placesLayer.graphics.removeAll();
      this.drawnIds = [];
      return;
    }

    const { add, keep } = mergePlaceGraphics(this.drawnIds, this.places);
    this.drawnIds = [...keep, ...add.map(place => place.id)];

    for (const place of add) {
      this.placesLayer.add(this.createPlaceGraphic(place));
    }

    for (const place of this.places) {
      if (keep.includes(place.id)) {
        this.updatePlaceGraphic(place);
      }
    }
  }

  private syncRoute(): void {
    if (this.routeGraphic) {
      this.routeLayer.remove(this.routeGraphic);
      this.routeGraphic = undefined;
    }

    if (this.stopOrder.length < 2) {
      return;
    }

    const paths = this.stopOrder
      .map(id => this.places.find(place => place.id === id))
      .filter((place): place is Place => !!place)
      .map(place => [place.location.longitude, place.location.latitude]);

    if (paths.length < 2) {
      return;
    }

    this.routeGraphic = new Graphic({
      geometry: new Polyline({ paths: [paths] }),
      symbol: this.lineSymbol,
    });
    this.routeLayer.add(this.routeGraphic);
  }

  private syncSelection(): void {
    for (const graphic of this.placesLayer.graphics.toArray()) {
      const id = graphic.attributes['id'] as string;
      graphic.symbol = id === this.selectedId ? this.selectedSymbol : this.markerSymbol;
    }
  }

  private createPlaceGraphic(place: Place): Graphic {
    return new Graphic({
      geometry: new Point({
        longitude: place.location.longitude,
        latitude: place.location.latitude,
      }),
      symbol: place.id === this.selectedId ? this.selectedSymbol : this.markerSymbol,
      attributes: { id: place.id, name: place.name, address: place.address ?? '' },
      popupTemplate: new PopupTemplate({
        title: '{name}',
        content: '{address}',
      }),
    });
  }

  private updatePlaceGraphic(place: Place): void {
    const graphic = this.placesLayer.graphics.find(
      existing => existing.attributes['id'] === place.id
    );
    if (!graphic) {
      return;
    }

    graphic.geometry = new Point({
      longitude: place.location.longitude,
      latitude: place.location.latitude,
    });
    graphic.attributes = { id: place.id, name: place.name, address: place.address ?? '' };
    graphic.symbol = place.id === this.selectedId ? this.selectedSymbol : this.markerSymbol;
  }

  private onMapClick(event: any): void {
    if (!this.view) {
      return;
    }

    if (this.needsMapPick) {
      const mapPoint = this.view.toMap(event);
      if (!mapPoint) {
        return;
      }
      const geo = webMercatorUtils.webMercatorToGeographic(mapPoint);
      const json = geo.toJSON();
      this.mapPick.emit({ latitude: json.y, longitude: json.x });
      return;
    }

    this.view.hitTest(event).then(response => {
      const hit = response.results.find(
        result => result.type === 'graphic' && result.graphic.layer === this.placesLayer
      );
      if (hit?.type === 'graphic') {
        const id = hit.graphic.attributes['id'] as string;
        if (id) {
          this.placeSelect.emit(id);
        }
      }
    });
  }
}
