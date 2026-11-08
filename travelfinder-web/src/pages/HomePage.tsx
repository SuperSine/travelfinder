import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Heading, Stack, Text, TextArea } from '@astryxdesign/core';
import './home-page.css';

export function HomePage() {
  const navigate = useNavigate();
  const [prompt, setPrompt] = useState('');
  const [geoStatus, setGeoStatus] = useState('Location not requested yet');

  const startPlanning = () => {
    const requestId = crypto.randomUUID();
    let latitude: number | null = null;
    let longitude: number | null = null;

    const goToPlan = () => {
      sessionStorage.setItem(
        'tf.pending',
        JSON.stringify({ requestId, prompt, latitude, longitude }),
      );
      navigate('/plan');
    };

    if (!navigator.geolocation) {
      setGeoStatus('Geolocation unavailable — pick a location on the map');
      goToPlan();
      return;
    }

    setGeoStatus('Requesting location…');
    navigator.geolocation.getCurrentPosition(
      position => {
        latitude = position.coords.latitude;
        longitude = position.coords.longitude;
        setGeoStatus(`Location: ${latitude.toFixed(4)}, ${longitude.toFixed(4)}`);
        goToPlan();
      },
      () => {
        setGeoStatus('Could not get location — pick a point on the map');
        goToPlan();
      },
    );
  };

  return (
    <Stack gap={4} className="home-page">
      <Heading level={2}>Plan a trip</Heading>
      <TextArea
        label="Trip description"
        isLabelHidden
        placeholder="Describe the trip"
        rows={4}
        value={prompt}
        onChange={value => setPrompt(value)}
      />
      <Text type="supporting">{geoStatus}</Text>
      <Button label="Start planning" variant="primary" onClick={startPlanning} />
    </Stack>
  );
}
