import { PageMeta } from "../components/PageMeta";
import { PricingCards } from "../components/PricingCards";
import { useI18n } from "../i18n";

export function PricingPage() {
  const { t } = useI18n();
  return <>
    <PageMeta title="Simple visit-based pricing" description="Compare Free, Visit Pass, and Trip Pass draft pricing for Park Pilot." />
    <section className="page-hero"><div className="container narrow"><span className="eyebrow">{t("DRAFT INTRODUCTORY PRICING")}</span><h1>{t("One pass. One better park day.")}</h1><p>{t("Park Pilot is visit-based, not subscription-first. Choose a single day or cover a multi-day trip.")}</p></div></section>
    <section className="section pricing-page"><div className="container"><div className="draft-banner" role="note"><strong>{t("Early pricing preview")}</strong><span>{t("These prices are introductory drafts for product review. Final pricing, park coverage, taxes, and pass terms may change before launch.")}</span></div><PricingCards />
      <div className="pricing-notes"><article><h2>{t("What counts as a park day?")}</h2><p>{t("A Visit Pass is intended for one date at one supported park, activated for that visit. Exact activation and transfer terms are still being tested.")}</p></article><article><h2>{t("Why no monthly subscription?")}</h2><p>{t("Most people need Park Pilot while traveling. Our initial model lets you pay for the visit rather than another recurring bill.")}</p></article><article><h2>{t("Can I browse before buying?")}</h2><p>{t("Yes. Free access is planned to include attraction information, current queue times, and basic park browsing.")}</p></article></div>
    </div></section>
  </>;
}
