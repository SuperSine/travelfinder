import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { UiButtonComponent } from '../../ui/ui-button.component';

@Component({
  selector: 'app-home',
  templateUrl: 'home.page.html',
  styleUrls: ['home.page.scss'],
  standalone: true,
  imports: [CommonModule, FormsModule, UiButtonComponent]
})
export class HomePage {
  prompt = '';
  geoStatus = 'Location not requested yet';
  needsMapPick = false;

  constructor(private router: Router) {}

  startPlanning(): void {
    const requestId = crypto.randomUUID();
    let latitude: number | null = null;
    let longitude: number | null = null;

    const navigate = () => {
      sessionStorage.setItem(
        'tf.pending',
        JSON.stringify({ requestId, prompt: this.prompt, latitude, longitude })
      );
      this.router.navigate(['/plan']);
    };

    if (!navigator.geolocation) {
      this.needsMapPick = true;
      this.geoStatus = 'Geolocation unavailable — pick a location on the map';
      navigate();
      return;
    }

    this.geoStatus = 'Requesting location…';
    navigator.geolocation.getCurrentPosition(
      (position) => {
        latitude = position.coords.latitude;
        longitude = position.coords.longitude;
        this.needsMapPick = false;
        this.geoStatus = `Location: ${latitude.toFixed(4)}, ${longitude.toFixed(4)}`;
        navigate();
      },
      () => {
        this.needsMapPick = true;
        this.geoStatus = 'Could not get location — pick a point on the map';
        navigate();
      }
    );
  }
}
