DELETE FROM public.queue_observations
WHERE park_id IN (
    SELECT id
    FROM public.parks
    WHERE source_park_id IN (4, 16, 17, 28, 30, 31, 274, 275)
);

DELETE FROM public.queue_collection_runs
WHERE park_id IN (
    SELECT id
    FROM public.parks
    WHERE source_park_id IN (4, 16, 17, 28, 30, 31, 274, 275)
);

DELETE FROM public.attractions
WHERE park_id IN (
    SELECT id
    FROM public.parks
    WHERE source_park_id IN (4, 16, 17, 28, 30, 31, 274, 275)
);

DELETE FROM public.lands
WHERE park_id IN (
    SELECT id
    FROM public.parks
    WHERE source_park_id IN (4, 16, 17, 28, 30, 31, 274, 275)
);

DELETE FROM public.parks
WHERE source_park_id IN (4, 16, 17, 28, 30, 31, 274, 275);
