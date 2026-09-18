import { useI18n } from "../i18n";

interface Props { title: string; description: string; noIndex?: boolean }

export function PageMeta({ title, description, noIndex }: Props) {
  const { t } = useI18n();
  const localizedTitle = `${t(title)} | Park Pilot`;
  const localizedDescription = t(description);
  return <>
    <title>{localizedTitle}</title>
    <meta name="description" content={localizedDescription} />
    {noIndex && <meta name="robots" content="noindex, nofollow" />}
    <meta property="og:title" content={localizedTitle} />
    <meta property="og:description" content={localizedDescription} />
  </>;
}