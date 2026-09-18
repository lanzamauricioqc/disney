import type { Translate } from "../i18n";
import { ApiError } from "./api";

const apiErrorMessages = {
  invalid_credentials: "The email or password is invalid.",
  already_bootstrapped: "An organization already exists.",
  invalid_invitation: "The invitation is invalid, expired, or already used.",
  invitation_not_created: "The invitation could not be created.",
  role_not_changed: "The role could not be changed.",
  user_not_deactivated: "The user could not be deactivated.",
  entitlement_not_assigned: "The visit credit could not be assigned.",
  company_billing_not_configured: "Company billing is not configured.",
} as const;

export function errorText(error: unknown, t: Translate): string {
  if (!(error instanceof ApiError)) {
    return error instanceof Error ? error.message : t("The request could not be completed.");
  }

  if (error.info.status === 0) {
    return t("Could not reach the Park Pilot company API. Confirm the API proxy and server configuration.");
  }

  const message = apiErrorMessages[error.info.title as keyof typeof apiErrorMessages];
  if (message) {
    return t(message);
  }

  if (error.info.status === 400 || error.info.status === 422) {
    return t("Some information is invalid. Review the form and try again.");
  }

  return t("Request failed ({status})", { status: error.info.status });
}
