import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../AuthContext";
export function ProtectedRoute() { const { session, ready } = useAuth(); const location = useLocation(); if (!ready) return null; return session ? <Outlet /> : <Navigate to="/login" replace state={{ from: location }} />; }
