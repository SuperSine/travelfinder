# Manual planning loop

1. Run API on http://localhost:1294 and web on http://localhost:4200.
2. Open /home at 375px and at 1280px.
3. Allow geolocation or pick a point on the map.
4. Send "one park day near me".
5. Confirm chat shows a spec or a clarification; if clarification, answer and resend.
6. Confirm the map plots points without wiping on the second places payload.
7. Confirm itinerary cards fill from plan_delta.
8. Confirm /detail shows the same stops.
9. With AZUREAI_API_KEY empty and XAI_API_KEY set, repeat once and confirm the loop still completes.
