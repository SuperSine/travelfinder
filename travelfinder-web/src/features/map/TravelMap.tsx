import { useEffect, useRef } from 'react';
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
import type { GeoPoint, Place } from '../../core/domain/models';
import { mergePlaceGraphics } from './place-graphics';
import { MapViewReadyGate } from './map-view-ready-gate';
import './travel-map.css';

export interface TravelMapProps {
  places: Place[];
  selectedId: string | null;
  stopOrder: string[];
  needsMapPick: boolean;
  onPlaceSelect: (placeId: string) => void;
  onMapPick: (point: GeoPoint) => void;
  onReady?: () => void;
}

export function TravelMap({
  places,
  selectedId,
  stopOrder,
  needsMapPick,
  onPlaceSelect,
  onMapPick,
  onReady,
}: TravelMapProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const viewRef = useRef<MapView | undefined>(undefined);
  const placesLayerRef = useRef(new GraphicsLayer({ id: 'places' }));
  const routeLayerRef = useRef(new GraphicsLayer({ id: 'route' }));
  const drawnIdsRef = useRef<string[]>([]);
  const routeGraphicRef = useRef<Graphic | undefined>(undefined);
  const gateRef = useRef(new MapViewReadyGate());
  const propsRef = useRef({ places, selectedId, stopOrder, needsMapPick, onMapPick, onPlaceSelect });

  propsRef.current = { places, selectedId, stopOrder, needsMapPick, onMapPick, onPlaceSelect };

  const markerSymbol = useRef(
    new SimpleMarkerSymbol({
      color: '#2563eb',
      size: 10,
      outline: { color: '#ffffff', width: 1 },
    }),
  ).current;

  const selectedSymbol = useRef(
    new SimpleMarkerSymbol({
      color: '#dc2626',
      size: 12,
      outline: { color: '#ffffff', width: 2 },
    }),
  ).current;

  const lineSymbol = useRef(
    new SimpleLineSymbol({
      color: '#2563eb',
      width: 3,
    }),
  ).current;

  const createPlaceGraphic = (place: Place, currentSelectedId: string | null): Graphic =>
    new Graphic({
      geometry: new Point({
        longitude: place.location.longitude,
        latitude: place.location.latitude,
      }),
      symbol:
        place.id === currentSelectedId ? selectedSymbol : markerSymbol,
      attributes: { id: place.id, name: place.name, address: place.address ?? '' },
      popupTemplate: new PopupTemplate({
        title: '{name}',
        content: '{address}',
      }),
    });

  const syncPlaces = () => {
    const layer = placesLayerRef.current;
    const { places: currentPlaces, selectedId: currentSelectedId } = propsRef.current;

    if (currentPlaces.length === 0) {
      layer.graphics.removeAll();
      drawnIdsRef.current = [];
      return;
    }

    const { add, keep } = mergePlaceGraphics(drawnIdsRef.current, currentPlaces);
    drawnIdsRef.current = [...keep, ...add.map(place => place.id)];

    for (const place of add) {
      layer.add(createPlaceGraphic(place, currentSelectedId));
    }

    for (const place of currentPlaces) {
      if (keep.includes(place.id)) {
        const graphic = layer.graphics.find(
          existing => existing.attributes['id'] === place.id,
        );
        if (!graphic) {
          continue;
        }
        graphic.geometry = new Point({
          longitude: place.location.longitude,
          latitude: place.location.latitude,
        });
        graphic.attributes = {
          id: place.id,
          name: place.name,
          address: place.address ?? '',
        };
        graphic.symbol =
          place.id === currentSelectedId ? selectedSymbol : markerSymbol;
      }
    }
  };

  const syncRoute = () => {
    const routeLayer = routeLayerRef.current;
    if (routeGraphicRef.current) {
      routeLayer.remove(routeGraphicRef.current);
      routeGraphicRef.current = undefined;
    }

    const { stopOrder: order, places: currentPlaces } = propsRef.current;
    if (order.length < 2) {
      return;
    }

    const paths = order
      .map(id => currentPlaces.find(place => place.id === id))
      .filter((place): place is Place => !!place)
      .map(place => [place.location.longitude, place.location.latitude]);

    if (paths.length < 2) {
      return;
    }

    routeGraphicRef.current = new Graphic({
      geometry: new Polyline({ paths: [paths] }),
      symbol: lineSymbol,
    });
    routeLayer.add(routeGraphicRef.current);
  };

  const syncSelection = () => {
    const { selectedId: currentSelectedId } = propsRef.current;
    for (const graphic of placesLayerRef.current.graphics.toArray()) {
      const id = graphic.attributes['id'] as string;
      graphic.symbol = id === currentSelectedId ? selectedSymbol : markerSymbol;
    }
  };

  const applySync = () => {
    syncPlaces();
    syncRoute();
    syncSelection();
  };

  useEffect(() => {
    const container = containerRef.current;
    if (!container) {
      return;
    }

    const webmap = new WebMap({
      basemap: 'topo-vector',
      layers: [placesLayerRef.current, routeLayerRef.current],
    });

    const view = new MapView({
      container,
      map: webmap,
      zoom: 13,
      center: [103.8198, 1.3521],
    });
    viewRef.current = view;

    let disposed = false;

    void view.when().then(() => {
      if (disposed) {
        return;
      }

      view.on('click', event => {
        const { needsMapPick: pick, onMapPick, onPlaceSelect } = propsRef.current;

        if (pick) {
          const mapPoint = view.toMap(event);
          if (!mapPoint) {
            return;
          }
          const geo = webMercatorUtils.webMercatorToGeographic(mapPoint);
          const json = geo.toJSON();
          onMapPick({ latitude: json.y, longitude: json.x });
          return;
        }

        view.hitTest(event).then(response => {
          const hit = response.results.find(
            result =>
              result.type === 'graphic' &&
              result.graphic.layer === placesLayerRef.current,
          );
          if (hit?.type === 'graphic') {
            const id = hit.graphic.attributes['id'] as string;
            if (id) {
              onPlaceSelect(id);
            }
          }
        });
      });

      const needsReplay = gateRef.current.markReady();
      if (needsReplay) {
        applySync();
      } else {
        applySync();
      }
      onReady?.();
    });

    return () => {
      disposed = true;
      view.destroy();
      viewRef.current = undefined;
    };
  }, [onReady]);

  useEffect(() => {
    if (!gateRef.current.shouldApply()) {
      gateRef.current.onChange();
      return;
    }
    syncPlaces();
  }, [places]);

  useEffect(() => {
    if (!gateRef.current.shouldApply()) {
      gateRef.current.onChange();
      return;
    }
    syncRoute();
  }, [stopOrder, places]);

  useEffect(() => {
    if (!gateRef.current.shouldApply()) {
      gateRef.current.onChange();
      return;
    }
    syncSelection();
  }, [selectedId]);

  return <div ref={containerRef} className="travel-map-container" />;
}
