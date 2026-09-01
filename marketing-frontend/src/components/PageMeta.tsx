interface Props { title: string; description: string; noIndex?: boolean }

export function PageMeta({ title, description, noIndex }: Props) {
  return <>
    <title>{title} | Park Pilot</title>
    <meta name="description" content={description} />
    {noIndex && <meta name="robots" content="noindex, nofollow" />}
    <meta property="og:title" content={`${title} | Park Pilot`} />
    <meta property="og:description" content={description} />
  </>;
}
