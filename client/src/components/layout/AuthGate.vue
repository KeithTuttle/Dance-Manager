<script setup lang="ts">
import { watch } from 'vue'
import { useDark } from '@vueuse/core'
import { useAuth, SignIn, updateClerkOptions } from '@clerk/vue'
import { dark as clerkDarkTheme } from '@clerk/ui/themes'
import AppShell from '@/components/layout/AppShell.vue'

// Only mounted when Clerk is configured (see App.vue), so useAuth always has a
// provider. While Clerk loads we show a neutral splash; once loaded we either
// render the app (signed in) or the sign-in card (signed out).
const { isLoaded, isSignedIn } = useAuth()

// Clerk renders its own DOM with its own default (light) theme and has no idea
// our app just went dark. It uses the @clerk/ui theming system, whose global
// appearance is set via updateClerkOptions({ appearance: { theme } }). This one
// call reaches every Clerk component (UserButton, SignIn, …) consistently —
// per-component `appearance` props don't cover UserButton's trigger/name. We
// re-apply whenever dark mode toggles or once Clerk finishes loading (in case
// the app booted straight into dark mode).
const isDark = useDark()
watch(
  [isDark, isLoaded],
  ([dark, loaded]) => {
    if (!loaded) return
    try {
      // The @clerk/ui `theme` appearance key is valid at runtime (per @clerk/vue's
      // own docs) but is missing from the shipped Appearance<Ui> type, so we cast.
      updateClerkOptions({ appearance: { theme: dark ? clerkDarkTheme : undefined } as never })
    } catch {
      // Clerk instance not ready yet; the next toggle will retry.
    }
  },
  { immediate: true },
)
</script>

<template>
  <div
    v-if="!isLoaded"
    class="flex h-screen w-full items-center justify-center bg-background text-sm text-muted-foreground"
  >
    Loading…
  </div>
  <AppShell v-else-if="isSignedIn" />
  <div v-else class="flex h-screen w-full items-center justify-center bg-background p-4">
    <SignIn />
  </div>
</template>
