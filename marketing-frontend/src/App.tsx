import { Route, Routes } from "react-router-dom";
import { Layout } from "./components/Layout";
import { LandingPage } from "./pages/LandingPage";
import { ProductPage } from "./pages/ProductPage";
import { PricingPage } from "./pages/PricingPage";
import { ComparePage } from "./pages/ComparePage";
import { CheckoutPage } from "./pages/CheckoutPage";
import { CompaniesPage } from "./pages/CompaniesPage";
import { NotFoundPage } from "./pages/NotFoundPage";
import { useI18n } from "./i18n";

export function App() {
  const { t } = useI18n();
  return <><a className="skip-link" href="#main-content">{t("Skip to content")}</a><Routes><Route element={<Layout />}>
    <Route index element={<LandingPage />} />
    <Route path="product" element={<ProductPage />} />
    <Route path="pricing" element={<PricingPage />} />
    <Route path="compare" element={<ComparePage />} />
    <Route path="checkout" element={<CheckoutPage />} />
    <Route path="companies" element={<CompaniesPage />} />
    <Route path="*" element={<NotFoundPage />} />
  </Route></Routes></>;
}
