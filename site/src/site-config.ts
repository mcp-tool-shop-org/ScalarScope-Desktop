import type { SiteConfig } from '@mcptoolshop/site-theme';

export const config: SiteConfig = {
  title: 'ScalarScope',
  description: 'ASPIRE Scalar Vortex Visualizer — .NET MAUI desktop app for comparing ML inference runs with scientific rigor.',
  logoBadge: 'SS',
  brandName: 'ScalarScope',
  repoUrl: 'https://github.com/mcp-tool-shop-org/ScalarScope-Desktop',
  footerText: 'MIT Licensed — built by <a href="https://github.com/mcp-tool-shop-org" style="color:var(--color-muted);text-decoration:underline">mcp-tool-shop-org</a>',

  hero: {
    badge: '.NET MAUI · Windows',
    headline: 'ML inference runs,',
    headlineAccent: 'compared with rigor.',
    description: 'Stop eyeballing logs. ScalarScope loads two TFRT traces side by side, fires canonical delta analysis only when differences are statistically meaningful, and exports cryptographically verified bundles your team can reproduce.',
    primaryCta: { href: '#quickstart', label: 'Get from Microsoft Store' },
    secondaryCta: { href: '#features', label: 'See features' },
    previews: [
      {
        label: 'Install',
        code: '# Microsoft Store (ID: 9P3HT1PHBKQK)\n# https://apps.microsoft.com/detail/9P3HT1PHBKQK\n\n# Or use VortexKit in your own .NET app:\ndotnet add package VortexKit',
      },
      {
        label: 'VortexKit',
        code: 'using VortexKit.Core;\n\n// Shared timeline across all canvases\nvar player = new PlaybackController { Duration = 10.0, Loop = true };\nplayer.TimeChanged += () =>\n{\n    trajectoryCanvas.CurrentTime = player.Time;\n    eigenCanvas.CurrentTime      = player.Time;\n    scalarsCanvas.CurrentTime    = player.Time;\n};',
      },
      {
        label: 'Export bundle',
        code: 'var exporter = new ExportService();\nawait exporter.ExportComparisonAsync(\n    baseline,\n    optimized,\n    outputPath: "results/run-42.scbundle"\n);\n// SHA-256 verified, frozen deltas,\n// full provenance metadata inside',
      },
    ],
  },

  sections: [
    {
      kind: 'features',
      id: 'features',
      title: 'Replace eyeballing with structure',
      subtitle: 'Every comparison is reproducible. Every delta is earned.',
      features: [
        {
          title: 'Apples-to-apples comparison',
          desc: 'Load two TFRT inference traces side by side. The TFRT preset auto-suppresses irrelevant metrics so your analysis stays focused on what actually changed between runs.',
        },
        {
          title: 'Canonical delta analysis',
          desc: 'Five delta types — ΔTc, ΔO, ΔF, ΔĀ, ΔTd — fire only when differences are statistically meaningful. No noise, no false signals, no manual threshold tuning.',
        },
        {
          title: 'Reproducible .scbundle exports',
          desc: 'Export cryptographically signed bundles with SHA-256 integrity, frozen deltas, and full provenance metadata. Open in Review mode and results are verified, not re-derived.',
        },
      ],
    },
    {
      kind: 'data-table',
      id: 'deltas',
      title: 'Five canonical delta types',
      subtitle: 'Each delta fires only when the difference clears its statistical threshold.',
      columns: ['Delta', 'Measures'],
      rows: [
        ['ΔTc', 'Compute time — wall-clock latency difference between runs'],
        ['ΔO', 'Output divergence — numerical difference in model outputs'],
        ['ΔF', 'Feature activation — changes in intermediate layer activations'],
        ['ΔĀ', 'Average metric shift — mean performance across the evaluation set'],
        ['ΔTd', 'Data throughput — tokens or samples processed per second'],
      ],
    },
    {
      kind: 'code-cards',
      id: 'quickstart',
      title: 'Quick start',
      cards: [
        {
          title: 'Install from Microsoft Store',
          code: '# Store ID: 9P3HT1PHBKQK\n# https://apps.microsoft.com/detail/9P3HT1PHBKQK\n\n1. Click "Compare Two Runs"\n2. Load baseline TFRT trace (before)\n3. Load optimized TFRT trace (after)\n4. Review deltas in the Compare tab\n5. Export .scbundle for reproducible sharing',
        },
        {
          title: 'Add VortexKit to your app',
          code: 'dotnet add package VortexKit\n\n// Reusable visualization framework:\n// - Time-synced playback across canvases\n// - Animated SkiaSharp rendering\n// - Comparison views + annotation overlays\n// - SVG/PNG export\n// - Semantic color system',
        },
        {
          title: 'Custom canvas with VortexKit',
          code: 'public class MyTrajectoryCanvas : AnimatedCanvas\n{\n    protected override void OnRender(\n        SKCanvas canvas,\n        SKImageInfo info,\n        double time)\n    {\n        // SkiaSharp rendering at current timeline position\n        // Automatically synchronized with other canvases\n        // via shared PlaybackController\n    }\n}',
        },
        {
          title: 'Build from source',
          code: 'git clone https://github.com/mcp-tool-shop-org/ScalarScope-Desktop\ncd ScalarScope-Desktop\n\n# Requires .NET 9 + MAUI workload\ndotnet workload install maui-windows\ndotnet build ScalarScope.sln\n\n# Run tests\ndotnet test',
        },
      ],
    },
    {
      kind: 'features',
      id: 'design',
      title: 'Designed for serious ML workflows',
      subtitle: 'Scientific rigor without the spreadsheet.',
      features: [
        {
          title: 'Review mode',
          desc: 'Open any .scbundle and results are cryptographically verified against the embedded SHA-256 hash — not re-derived from raw data. Share bundles and know both parties see identical results.',
        },
        {
          title: 'Privacy first',
          desc: 'Zero telemetry, zero analytics, zero network calls. All comparison data stays on your machine unless you explicitly export a bundle. No accounts, no subscriptions.',
        },
        {
          title: 'VortexKit for your own tools',
          desc: 'The visualization framework powering ScalarScope is a standalone NuGet package. Build time-synced animated canvases, comparison views, and export pipelines into any .NET MAUI app.',
        },
      ],
    },
  ],
};
