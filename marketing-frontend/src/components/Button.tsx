import type { ButtonHTMLAttributes, ReactNode } from "react";
import { Link } from "react-router-dom";

type Common = { children: ReactNode; variant?: "primary" | "secondary" | "text"; className?: string };
type Props = Common & ({ to: string; type?: never } | ({ to?: never } & ButtonHTMLAttributes<HTMLButtonElement>));

export function Button({ children, variant = "primary", className = "", ...props }: Props) {
  const classes = `button button--${variant} ${className}`.trim();
  if ("to" in props && props.to) return <Link className={classes} to={props.to}>{children}</Link>;
  return <button className={classes} {...(props as ButtonHTMLAttributes<HTMLButtonElement>)}>{children}</button>;
}
