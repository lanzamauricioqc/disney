INSERT INTO public.parks (source_park_id, name, timezone)
VALUES
    (5, 'Epcot', 'America/New_York'),
    (6, 'Disney Magic Kingdom', 'America/New_York'),
    (7, 'Disney Hollywood Studios', 'America/New_York'),
    (8, 'Animal Kingdom', 'America/New_York')
ON CONFLICT (source_park_id) DO UPDATE
SET name = EXCLUDED.name,
    timezone = EXCLUDED.timezone,
    updated_at = now();
