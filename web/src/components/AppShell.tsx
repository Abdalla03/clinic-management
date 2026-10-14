import { useState } from "react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../features/auth/useAuth";
import { clinicName } from "../lib/branding";
import { cn } from "../lib/cn";
import { useI18n } from "../lib/i18n/useI18n";
import type { TranslationKey } from "../lib/i18n/translations";
import { ClinicalStaffRoles, RolePatient, type Role } from "../lib/roles";
import { AmbientBackground } from "./AmbientBackground";
import { LanguageToggle } from "./LanguageToggle";
import { ThemeToggle } from "./ThemeToggle";
import {
  IconCalendar,
  IconChart,
  IconClipboard,
  IconClock,
  IconDashboard,
  IconFileText,
  IconHeart,
  IconLogOut,
  IconMenu,
  IconPill,
  IconShield,
  IconUsers,
} from "./icons";

interface NavItem {
  to: string;
  end?: boolean;
  labelKey: TranslationKey;
  icon: React.ReactNode;
  allowedRoles?: readonly Role[];
}

const navItems: NavItem[] = [
  { to: "/", end: true, labelKey: "nav.dashboard", icon: <IconDashboard /> },
  {
    to: "/patients",
    labelKey: "nav.patients",
    icon: <IconUsers />,
    allowedRoles: ClinicalStaffRoles,
  },
  {
    to: "/appointments",
    labelKey: "nav.appointments",
    icon: <IconCalendar />,
    allowedRoles: ClinicalStaffRoles,
  },
  {
    to: "/appointments/me",
    labelKey: "nav.appointments",
    icon: <IconCalendar />,
    allowedRoles: [RolePatient],
  },
  {
    to: "/visits",
    labelKey: "nav.visits",
    icon: <IconClipboard />,
    allowedRoles: ClinicalStaffRoles,
  },
  {
    to: "/prescriptions",
    labelKey: "nav.prescriptions",
    icon: <IconPill />,
    allowedRoles: ClinicalStaffRoles,
  },
  {
    to: "/visits/me",
    labelKey: "nav.myVisits",
    icon: <IconClipboard />,
    allowedRoles: [RolePatient],
  },
  {
    to: "/prescriptions/me",
    labelKey: "nav.myPrescriptions",
    icon: <IconPill />,
    allowedRoles: [RolePatient],
  },
  {
    to: "/invoices",
    end: true,
    labelKey: "nav.invoices",
    icon: <IconFileText />,
    allowedRoles: ClinicalStaffRoles,
  },
  {
    to: "/invoices/me",
    labelKey: "nav.invoices",
    icon: <IconFileText />,
    allowedRoles: [RolePatient],
  },
  {
    to: "/reports",
    labelKey: "nav.reports",
    icon: <IconChart />,
    allowedRoles: ClinicalStaffRoles,
  },
  {
    to: "/doctors/schedule",
    labelKey: "nav.schedules",
    icon: <IconClock />,
    allowedRoles: ClinicalStaffRoles,
  },
  {
    to: "/me",
    labelKey: "nav.myHealth",
    icon: <IconHeart />,
    allowedRoles: [RolePatient],
  },
  {
    to: "/account/security",
    labelKey: "nav.security",
    icon: <IconShield />,
  },
];

function roleLabelKey(role: string): TranslationKey | undefined {
  switch (role) {
    case "Doctor":
      return "role.doctor";
    case "Secretary":
      return "role.secretary";
    case "Patient":
      return "role.patient";
    default:
      return undefined;
  }
}

export function AppShell() {
  const { user, roles, signOut } = useAuth();
  const { t, lang } = useI18n();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);

  const visibleItems = navItems.filter(
    (item) => !item.allowedRoles || item.allowedRoles.some((role) => roles.includes(role)),
  );

  const initials = (user?.fullName ?? "?")
    .split(" ")
    .filter(Boolean)
    .map((part) => part[0])
    .slice(0, 2)
    .join("")
    .toUpperCase();

  const primaryRole = ClinicalStaffRoles.find((role) => roles.includes(role)) ?? (roles.includes(RolePatient) ? RolePatient : undefined);
  const roleLabel = primaryRole ? t(roleLabelKey(primaryRole) ?? "role.doctor") : undefined;

  const handleSignOut = async () => {
    await signOut();
    navigate("/login", { replace: true });
  };

  return (
    <div className="relative min-h-screen">
      <AmbientBackground variant="app" />
      {open ? (
        <button
          type="button"
          aria-label={t("common.close")}
          onClick={() => setOpen(false)}
          className="fixed inset-0 z-30 bg-slate-900/40 lg:hidden"
        />
      ) : null}

      <aside
        className={cn(
          "fixed inset-y-0 start-0 z-40 flex w-64 flex-col bg-slate-900 text-slate-300 transition-transform duration-200 lg:translate-x-0",
          open ? "translate-x-0" : "-translate-x-full rtl:translate-x-full",
        )}
      >
        <div className="flex items-center gap-2 px-5 py-5">
          <div className="flex size-9 items-center justify-center rounded-xl bg-primary-600 text-white">
            <IconHeart className="size-5" />
          </div>
          <div className="min-w-0">
            <p className="truncate text-sm font-bold text-white">{clinicName}</p>
            <p className="text-xs text-slate-400">{t("brand.tagline")}</p>
          </div>
        </div>

        <nav className="flex-1 overflow-y-auto px-3 py-2">
          <ul className="flex flex-col gap-1">
            {visibleItems.map((item) => (
              <li key={item.to}>
                <NavLink
                  to={item.to}
                  end={item.end}
                  onClick={() => setOpen(false)}
                  className={({ isActive }) =>
                    cn(
                      "flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors",
                      isActive
                        ? "bg-primary-600 text-white shadow-sm"
                        : "text-slate-300 hover:bg-slate-800 hover:text-white",
                    )
                  }
                >
                  {item.icon}
                  {t(item.labelKey)}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        <div className="border-t border-slate-800 p-3">
          <button
            type="button"
            onClick={handleSignOut}
            className="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium text-slate-300 transition-colors hover:bg-slate-800 hover:text-white"
          >
            <IconLogOut />
            {t("auth.logout")}
          </button>
        </div>
      </aside>

      <div className="lg:ps-64">
        <header className="glass sticky top-0 z-20 flex h-16 items-center gap-3 border-b border-slate-200 px-4 lg:px-8 dark:border-slate-800">
          <button
            type="button"
            onClick={() => setOpen(true)}
            className="inline-flex size-9 items-center justify-center rounded-lg text-slate-600 hover:bg-slate-100 lg:hidden dark:text-slate-300 dark:hover:bg-slate-800"
            aria-label={t("common.menu")}
          >
            <IconMenu />
          </button>

          <div className="flex-1" />

          <LanguageToggle />
          <ThemeToggle />

          <div className="flex items-center gap-3 rounded-lg border border-slate-200 bg-white py-1 ps-1 pe-3 shadow-sm">
            <span className="flex size-8 items-center justify-center rounded-md bg-primary-100 text-xs font-bold text-primary-700">
              {initials}
            </span>
            <div className="hidden leading-tight sm:block">
              <p className="text-sm font-semibold text-slate-900">{user?.fullName}</p>
              {roleLabel ? <p className="text-xs text-slate-500" lang={lang}>{roleLabel}</p> : null}
            </div>
          </div>
        </header>

        <main className="mx-auto w-full max-w-6xl px-4 py-6 motion-safe:animate-fade-up lg:px-8 lg:py-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

export default AppShell;