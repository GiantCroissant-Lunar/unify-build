import * as vscode from "vscode";
import * as path from "path";
import * as fs from "fs";

/** Property documentation for build.config.json hover provider */
const PROPERTY_DOCS: Record<string, string> = {
  version:
    "The explicit version string for the build. Overrides versionEnv if set.",
  versionEnv:
    "Environment variable name to read the version from (e.g., GitVersion_MajorMinorPatch).",
  artifactsVersion:
    "Version string used for artifact naming. Defaults to the resolved version.",
  solution: "Relative path to the .sln file. Auto-detected if not specified.",
  projectGroups:
    "Named groups of projects with shared build actions. Each key is a group name.",
  sourceDir:
    "Relative path to the directory containing projects for this group.",
  action:
    'Build action for the group: "compile", "pack", or "publish".',
  include:
    "Array of project name patterns to include in this group. Supports glob patterns.",
  exclude:
    "Array of project name patterns to exclude from this group. Supports glob patterns.",
  compileProjects:
    "Legacy: Array of project paths to compile. Prefer projectGroups instead.",
  publishProjects:
    "Legacy: Array of project paths to publish. Prefer projectGroups instead.",
  packProjects:
    "Legacy: Array of project paths to pack. Prefer projectGroups instead.",
  nativeBuild:
    "Configuration for CMake-based native C++ builds.",
  rustBuild:
    "Configuration for Rust/Cargo builds.",
  goBuild: "Configuration for Go builds.",
  unityBuild: "Configuration for Unity project builds.",
  performance:
    "Performance settings: incremental builds, caching, change detection.",
  observability:
    "Observability settings: build metrics, telemetry.",
  packageManagement:
    "Package management: multi-registry publishing, signing, SBOM, retention.",
  enabled: "Whether this build type is enabled.",
  cmakeSourceDir: "Path to the directory containing CMakeLists.txt.",
  cmakeBuildDir: "Path to the CMake build output directory.",
  cmakePreset: "CMake preset name to use for configure and build.",
  cmakeOptions: "Additional CMake command-line options.",
  buildConfig:
    'CMake build configuration: "Debug", "Release", "RelWithDebInfo", "MinSizeRel".',
  autoDetectVcpkg:
    "Automatically detect and use vcpkg toolchain file if present.",
  outputDir: "Directory for build output artifacts.",
  artifactPatterns:
    "Glob patterns for files to collect as build artifacts.",
  customCommands: "Custom shell commands to execute for the native build.",
  platform:
    'Platform-specific build configuration: "windows", "linux", "macos".',
  cargoManifestDir: "Path to the directory containing Cargo.toml.",
  profile:
    'Cargo build profile: "debug", "release", or a custom profile name.',
  features: "Cargo features to enable during the build.",
  targetTriple:
    'Rust target triple (e.g., "x86_64-pc-windows-msvc").',
  goModuleDir: "Path to the directory containing go.mod.",
  buildFlags: "Additional flags passed to go build.",
  outputBinary: "Name of the output binary.",
  envVars:
    "Environment variables for the Go build (e.g., GOOS, GOARCH).",
  targetFramework:
    'Unity target framework (e.g., "netstandard2.1").',
  unityProjectRoot: "Path to the Unity project root directory.",
  packages: "Unity package mappings for build output.",
  enableCache: "Enable local build output caching.",
  cacheDir:
    'Directory for the build cache. Defaults to "build/_cache".',
  enableChangeDetection:
    "Enable file-timestamp-based change detection for incremental builds.",
  enableMetrics: "Enable build metrics collection.",
  metricsOutputDir: "Directory for metrics report output.",
  metricsFormat: 'Metrics export format: "json" or "csv".',
  enableTelemetry:
    "Enable anonymous local-only telemetry. Disabled by default.",
  registries: "Array of NuGet registries to publish packages to.",
  signing: "NuGet package signing configuration.",
  sbom: "Software Bill of Materials generation configuration.",
  retention: "Package retention policy for local feed cleanup.",
};

// ─── Tree View ───────────────────────────────────────────────────────────────

interface ProjectGroup {
  name: string;
  sourceDir?: string;
  action?: string;
  include?: string[];
  exclude?: string[];
}

class ProjectGroupItem extends vscode.TreeItem {
  constructor(
    public readonly group: ProjectGroup,
    public readonly collapsibleState: vscode.TreeItemCollapsibleState
  ) {
    super(group.name, collapsibleState);
    this.tooltip = `${group.name} — ${group.action ?? "compile"}`;
    this.description = group.action ?? "compile";
    this.contextValue = "projectGroup";
    this.iconPath = new vscode.ThemeIcon("folder-library");
  }
}

class ProjectItem extends vscode.TreeItem {
  constructor(public readonly projectPattern: string) {
    super(projectPattern, vscode.TreeItemCollapsibleState.None);
    this.iconPath = new vscode.ThemeIcon("file-code");
    this.contextValue = "project";
  }
}

export class ProjectGroupTreeProvider
  implements vscode.TreeDataProvider<vscode.TreeItem>
{
  private _onDidChangeTreeData = new vscode.EventEmitter<
    vscode.TreeItem | undefined | void
  >();
  readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

  private groups: ProjectGroup[] = [];

  refresh(): void {
    this.groups = this.loadProjectGroups();
    this._onDidChangeTreeData.fire();
  }

  getTreeItem(element: vscode.TreeItem): vscode.TreeItem {
    return element;
  }

  getChildren(element?: vscode.TreeItem): vscode.ProviderResult<vscode.TreeItem[]> {
    if (!element) {
      this.groups = this.loadProjectGroups();
      if (this.groups.length === 0) {
        return [
          new vscode.TreeItem("No build.config.json found"),
        ];
      }
      return this.groups.map(
        (g) =>
          new ProjectGroupItem(
            g,
            g.include && g.include.length > 0
              ? vscode.TreeItemCollapsibleState.Collapsed
              : vscode.TreeItemCollapsibleState.None
          )
      );
    }

    if (element instanceof ProjectGroupItem && element.group.include) {
      return element.group.include.map((p) => new ProjectItem(p));
    }

    return [];
  }

  private loadProjectGroups(): ProjectGroup[] {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    if (!workspaceFolders) {
      return [];
    }

    for (const folder of workspaceFolders) {
      const configPath = path.join(folder.uri.fsPath, "build.config.json");
      if (fs.existsSync(configPath)) {
        try {
          const raw = fs.readFileSync(configPath, "utf-8");
          const config = JSON.parse(raw);
          if (config.projectGroups && typeof config.projectGroups === "object") {
            return Object.entries(config.projectGroups).map(
              ([name, value]: [string, any]) => ({
                name,
                sourceDir: value.sourceDir,
                action: value.action,
                include: value.include,
                exclude: value.exclude,
              })
            );
          }
        } catch {
          // Ignore parse errors — the JSON validation will surface them
        }
      }
    }
    return [];
  }
}

// ─── Hover Provider ──────────────────────────────────────────────────────────

class BuildConfigHoverProvider implements vscode.HoverProvider {
  provideHover(
    document: vscode.TextDocument,
    position: vscode.Position
  ): vscode.ProviderResult<vscode.Hover> {
    // Only activate for build.config.json files
    if (!document.fileName.endsWith("build.config.json")) {
      return undefined;
    }

    const wordRange = document.getWordRangeAtPosition(position, /"\w+"/);
    if (!wordRange) {
      return undefined;
    }

    const word = document.getText(wordRange).replace(/"/g, "");
    const doc = PROPERTY_DOCS[word];
    if (!doc) {
      return undefined;
    }

    const markdown = new vscode.MarkdownString();
    markdown.appendMarkdown(`**\`${word}\`** — ${doc}\n\n`);
    markdown.appendMarkdown(
      `[Configuration Reference](https://unifybuild.github.io/docs/configuration-reference)`
    );
    return new vscode.Hover(markdown, wordRange);
  }
}

// ─── Command Handlers ────────────────────────────────────────────────────────

function runInTerminal(name: string, command: string): void {
  const terminal = vscode.window.createTerminal(name);
  terminal.show();
  terminal.sendText(command);
}

async function handleInit(): Promise<void> {
  const options = await vscode.window.showQuickPick(
    [
      { label: "Default", description: "Create a default build.config.json" },
      {
        label: "Library Template",
        description: "Optimized for .NET library projects",
      },
      {
        label: "Application Template",
        description: "Optimized for .NET application projects",
      },
      {
        label: "Interactive",
        description: "Step-by-step interactive wizard",
      },
    ],
    { placeHolder: "Select init mode" }
  );

  if (!options) {
    return;
  }

  let command = "dotnet unify-build init";
  switch (options.label) {
    case "Library Template":
      command += " --template library";
      break;
    case "Application Template":
      command += " --template application";
      break;
    case "Interactive":
      command += " --interactive";
      break;
  }

  runInTerminal("UnifyBuild Init", command);
}

function handleValidate(): void {
  runInTerminal("UnifyBuild Validate", "dotnet unify-build validate");
}

function handleDoctor(): void {
  runInTerminal("UnifyBuild Doctor", "dotnet unify-build doctor");
}

// ─── Activation ──────────────────────────────────────────────────────────────

export function activate(context: vscode.ExtensionContext): void {
  // Set context for conditional tree view visibility
  const workspaceFolders = vscode.workspace.workspaceFolders;
  if (workspaceFolders) {
    for (const folder of workspaceFolders) {
      const configPath = path.join(folder.uri.fsPath, "build.config.json");
      if (fs.existsSync(configPath)) {
        vscode.commands.executeCommand(
          "setContext",
          "unifybuild.configFound",
          true
        );
        break;
      }
    }
  }

  // Register tree view
  const treeProvider = new ProjectGroupTreeProvider();
  vscode.window.registerTreeDataProvider("unifybuildProjects", treeProvider);

  // Register commands
  context.subscriptions.push(
    vscode.commands.registerCommand("unifybuild.init", handleInit),
    vscode.commands.registerCommand("unifybuild.validate", handleValidate),
    vscode.commands.registerCommand("unifybuild.doctor", handleDoctor),
    vscode.commands.registerCommand("unifybuild.refreshProjects", () =>
      treeProvider.refresh()
    )
  );

  // Register hover provider for build.config.json
  context.subscriptions.push(
    vscode.languages.registerHoverProvider(
      { language: "json", pattern: "**/build.config.json" },
      new BuildConfigHoverProvider()
    )
  );

  // Watch for build.config.json changes to refresh tree view
  const watcher = vscode.workspace.createFileSystemWatcher(
    "**/build.config.json"
  );
  watcher.onDidChange(() => treeProvider.refresh());
  watcher.onDidCreate(() => {
    vscode.commands.executeCommand(
      "setContext",
      "unifybuild.configFound",
      true
    );
    treeProvider.refresh();
  });
  watcher.onDidDelete(() => {
    treeProvider.refresh();
  });
  context.subscriptions.push(watcher);
}

export function deactivate(): void {
  // Nothing to clean up
}
