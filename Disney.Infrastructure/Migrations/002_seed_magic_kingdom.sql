INSERT INTO public.parks (source_park_id, name, timezone)
VALUES (6, 'Magic Kingdom', 'America/New_York')
ON CONFLICT (source_park_id) DO NOTHING;
