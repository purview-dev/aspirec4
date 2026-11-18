// The Purview.BuildSdk agent-skill commands: a thin wrapper over the standard skills CLI
// (`bun x skills@1` by default), run by the skills-* recipes in skills.just.
//
// Nothing here needs .NET. There is no solution to evaluate, no package to restore and no project
// that has to build, so a repository whose build is broken can still install the skills that might
// help fix it.
//
//   add <source>       skills add "<source>" --skill "*" - append '#<ref>' to the source to pin it
//   install [source]   add <source>, or restore skills-lock.json with skills experimental_install
//   update             re-add every unique skills-lock.json source at its latest, with every skill
//   check              fail when a skill directory exists for an agent that is not selected
//   fix                remove the skills CLI's links from those directories
//
// Settings, highest first: command-line options, PURVIEW_SKILLS_* environment variables, the
// "Skills" section of .config/purview-build-sdk.json, then the defaults below. Every command runs
// from the repository root, because skills-lock.json and the agent directories belong to the
// repository rather than to a project.
//
// skills.mts is the source. skills.mjs is generated from it by Node's own type stripper, for a
// machine with Node but neither Bun nor Deno: edit this file, then run `just skills-script-sync`.
// Keep to erasable TypeScript (no enums, namespaces or parameter properties) so both stay possible.

import { spawnSync } from "node:child_process";
import { existsSync, lstatSync, readdirSync, readFileSync, rmdirSync, statSync, unlinkSync } from "node:fs";
import { delimiter, dirname, join, relative, resolve } from "node:path";
import process from "node:process";
import { parseArgs } from "node:util";

type AgentDirectory = { path: string; agents: string[] };

type Settings = {
	root: string;
	configPath: string;
	agents: string[];
	runner: string;
	runnerCandidates: string[];
	packageSpec: string;
	command: string;
	lockFile: string;
	dryRun: boolean;
	agentDirectories: AgentDirectory[];
};

type ConfigSection = Record<string, unknown>;

const configKeys: string[] = [
	"Agents",
	"Runner",
	"RunnerCandidates",
	"Package",
	"Command",
	"LockFile",
	"DryRun",
	"AgentDirectories",
];

// Each package runner spells "run this npm package" its own way. Every form was checked against the
// real tool, because a wrong one is a command that simply does not run. A runner missing from this
// table still works: it falls through to '<runner> <package>', the form most new runners adopt.
const launchers: Record<string, (spec: string) => string> = {
	skills: () => "skills",
	npx: (spec: string) => `npx -y ${spec}`,
	bunx: (spec: string) => `bunx ${spec}`,
	bun: (spec: string) => `bun x ${spec}`,
	pnpm: (spec: string) => `pnpm dlx ${spec}`,
	yarn: (spec: string) => `yarn dlx ${spec}`,
	deno: (spec: string) => `deno run -A npm:${spec}`,
};

// Where the skills CLI writes a project-scope install for each agent, from its own agent table
// (skills 1.7.6, docs/agents.md). Only check and fix read this, and only when agents are selected.
// The CLI adds agents independently of this script; add one through Skills.AgentDirectories in
// .config/purview-build-sdk.json rather than waiting for a release.
const defaultAgentDirectories: AgentDirectory[] = [
	{
		path: ".agents/skills",
		agents: [
			"amp",
			"antigravity",
			"antigravity-cli",
			"cline",
			"codex",
			"cursor",
			"deepagents",
			"dexto",
			"droid",
			"firebender",
			"fx",
			"gemini-cli",
			"github-copilot",
			"kilo",
			"kimi-code-cli",
			"loaf",
			"opencode",
			"pi",
			"promptscript",
			"replit",
			"sarvam-code",
			"universal",
			"warp",
			"zed",
		],
	},
	{ path: ".adal/skills", agents: ["adal"] },
	{ path: ".aider-desk/skills", agents: ["aider-desk"] },
	{ path: ".augment/skills", agents: ["augment"] },
	{ path: ".autohand/skills", agents: ["autohand-code"] },
	{ path: ".bob/skills", agents: ["bob"] },
	{ path: ".claude/skills", agents: ["claude-code"] },
	{ path: ".codeartsdoer/skills", agents: ["codearts-agent"] },
	{ path: ".codebuddy/skills", agents: ["codebuddy"] },
	{ path: ".codemaker/skills", agents: ["codemaker"] },
	{ path: ".codestudio/skills", agents: ["codestudio"] },
	{ path: ".commandcode/skills", agents: ["command-code"] },
	{ path: ".continue/skills", agents: ["continue"] },
	{ path: ".cortex/skills", agents: ["cortex"] },
	{ path: ".crush/skills", agents: ["crush"] },
	{ path: ".devin/skills", agents: ["devin"] },
	{ path: ".forge/skills", agents: ["forgecode"] },
	{ path: ".goose/skills", agents: ["goose"] },
	{ path: ".grok/skills", agents: ["grok"] },
	{ path: ".hermes/skills", agents: ["hermes-agent"] },
	{ path: ".iflow/skills", agents: ["iflow-cli"] },
	{ path: ".inferencesh/skills", agents: ["inference-sh"] },
	{ path: ".jazz/skills", agents: ["jazz"] },
	{ path: ".junie/skills", agents: ["junie"] },
	{ path: ".kimchi/skills", agents: ["kimchi"] },
	{ path: ".kiro/skills", agents: ["kiro-cli"] },
	{ path: ".kode/skills", agents: ["kode"] },
	{ path: ".lingma/skills", agents: ["lingma"] },
	{ path: ".mcpjam/skills", agents: ["mcpjam"] },
	{ path: ".minimax/skills", agents: ["minimax-code"] },
	{ path: ".moxby/skills", agents: ["moxby"] },
	{ path: ".mux/skills", agents: ["mux"] },
	{ path: ".neovate/skills", agents: ["neovate"] },
	{ path: ".ona/skills", agents: ["ona"] },
	{ path: ".openhands/skills", agents: ["openhands"] },
	{ path: ".pochi/skills", agents: ["pochi"] },
	{ path: ".posit/assistant/skills", agents: ["posit-assistant"] },
	{ path: ".qoder/skills", agents: ["qoder", "qoder-cn"] },
	{ path: ".qwen/skills", agents: ["qwen-code"] },
	{ path: ".reasonix/skills", agents: ["reasonix"] },
	{ path: ".roo/skills", agents: ["roo"] },
	{ path: ".rovodev/skills", agents: ["rovodev"] },
	{ path: ".tabnine/agent/skills", agents: ["tabnine-cli"] },
	{ path: ".terramind/skills", agents: ["terramind"] },
	{ path: ".tinycloud/skills", agents: ["tinycloud"] },
	{ path: ".trae/skills", agents: ["trae", "trae-cn"] },
	{ path: ".vibe/skills", agents: ["mistral-vibe"] },
	{ path: ".windsurf/skills", agents: ["windsurf"] },
	{ path: ".zcode/skills", agents: ["zcode"] },
	{ path: ".zencoder/skills", agents: ["zencoder", "zenflow"] },
	{ path: "agent/skills", agents: ["eve"] },
	{ path: "data/skills", agents: ["astrbot"] },
	// openclaw installs into a root 'skills/', the folder a repository authors its own skills in. It is
	// listed so selecting openclaw is not an unknown agent, but the authored folder is never touched.
	{ path: "skills", agents: ["openclaw"] },
];

const usage: string = `Usage: skills.mts <command> [options]

Commands:
  add <source>        Install every skill a source has (append '#<ref>' to pin it)
  install [source]    Install from a source, or restore skills-lock.json
  update              Re-add every skills-lock.json source at its latest
  check               Fail when a skill directory exists for an unselected agent
  fix                 Remove the skills CLI's links from those directories

Options:
  --root <path>       Repository root (default: the nearest folder holding .git)
  --config <path>     Settings file (default: .config/purview-build-sdk.json)
  --agents <list>     Agents to install to, separated by ',', ';' or spaces
  --runner <name>     Package runner: bun, npx, bunx, pnpm, yarn, deno or skills
  --command <cmd>     Launch the skills CLI with this command instead
  --package <spec>    Package the launcher runs (default: skills@1)
  --lock-file <path>  skills-lock.json location (default: the repository root)
  --dry-run           Print the skills CLI commands without running them
`;

class SkillsError extends Error {}

let failed = false;

function report(code: string, message: string): void {
	console.error(`error ${code}: ${message}`);
	failed = true;
}

function warn(code: string, message: string): void {
	console.error(`warning ${code}: ${message}`);
}

function info(message: string): void {
	console.log(message);
}

function environment(name: string): string | undefined {
	const value = process.env[name];
	return value === undefined || value.trim() === "" ? undefined : value.trim();
}

function splitList(value: string): string[] {
	return value
		.replace(/^[\s,;"']+|[\s,;"']+$/g, "")
		.split(/[\s,;]+/)
		.filter((entry: string) => entry.length > 0);
}

function quote(value: string): string {
	if (value.includes('"')) throw new SkillsError(`'${value}' contains a double quote, which cannot be passed safely.`);
	return `"${value}"`;
}

function unique(values: string[]): string[] {
	const seen: Set<string> = new Set();
	return values.filter((value: string) => {
		const key = value.toLowerCase();
		if (seen.has(key)) return false;
		seen.add(key);
		return true;
	});
}

// The nearest ancestor holding '.git' - a directory in a clone, a file in a worktree or a submodule.
// The recipes always pass --root; this only matters when the script is run by hand.
function findRepositoryRoot(start: string): string {
	for (let directory = resolve(start); ; directory = dirname(directory)) {
		if (existsSync(join(directory, ".git"))) return directory;
		if (dirname(directory) === directory) return resolve(start);
	}
}

function readConfig(configPath: string): ConfigSection {
	if (!existsSync(configPath)) return {};

	let document: unknown;
	try {
		document = JSON.parse(readFileSync(configPath, "utf8"));
	} catch (exception) {
		throw new SkillsError(`PRSKL0018 '${configPath}' is not valid JSON: ${(exception as Error).message}`);
	}

	const section = (document as Record<string, unknown> | null)?.["Skills"];
	if (section === undefined) return {};
	if (section === null || typeof section !== "object" || Array.isArray(section))
		throw new SkillsError(`PRSKL0018 '${configPath}': "Skills" must be an object.`);

	for (const key of Object.keys(section)) {
		if (!configKeys.includes(key))
			warn("PRSKL0019", `'${configPath}': "Skills.${key}" is not a setting and is ignored. Known: ${configKeys.join(", ")}.`);
	}

	return section as ConfigSection;
}

function configString(config: ConfigSection, key: string, configPath: string): string | undefined {
	const value = config[key];
	if (value === undefined || value === null) return undefined;
	if (typeof value !== "string") throw new SkillsError(`PRSKL0018 '${configPath}': "Skills.${key}" must be a string.`);
	return value.trim() === "" ? undefined : value.trim();
}

function configList(config: ConfigSection, key: string, configPath: string): string[] | undefined {
	const value = config[key];
	if (value === undefined || value === null) return undefined;
	if (typeof value === "string") return splitList(value);
	if (Array.isArray(value) && value.every((entry: unknown) => typeof entry === "string"))
		return value.flatMap((entry: string) => splitList(entry));
	throw new SkillsError(`PRSKL0018 '${configPath}': "Skills.${key}" must be a string or an array of strings.`);
}

function configBoolean(config: ConfigSection, key: string, configPath: string): boolean | undefined {
	const value = config[key];
	if (value === undefined || value === null) return undefined;
	if (typeof value !== "boolean") throw new SkillsError(`PRSKL0018 '${configPath}': "Skills.${key}" must be true or false.`);
	return value;
}

function configAgentDirectories(config: ConfigSection, configPath: string): AgentDirectory[] {
	const value = config["AgentDirectories"];
	if (value === undefined || value === null) return [];
	if (typeof value !== "object" || Array.isArray(value))
		throw new SkillsError(`PRSKL0018 '${configPath}': "Skills.AgentDirectories" must map a directory to its agents.`);

	return Object.entries(value as Record<string, unknown>).map(([path, agents]: [string, unknown]) => {
		if (typeof agents === "string") return { path, agents: splitList(agents) };
		if (Array.isArray(agents) && agents.every((entry: unknown) => typeof entry === "string"))
			return { path, agents: agents as string[] };
		throw new SkillsError(`PRSKL0018 '${configPath}': "Skills.AgentDirectories.${path}" must be a string or an array of strings.`);
	});
}

function parseBoolean(value: string | undefined): boolean | undefined {
	if (value === undefined) return undefined;
	return ["1", "true", "yes", "on"].includes(value.toLowerCase());
}

function resolveSettings(values: Record<string, string | boolean | undefined>): Settings {
	const option = (name: string): string | undefined => {
		const value = values[name];
		return typeof value === "string" && value.trim() !== "" ? value.trim() : undefined;
	};

	const root = resolve(option("root") ?? findRepositoryRoot(process.cwd()));
	const configPath = resolve(root, option("config") ?? environment("PURVIEW_SKILLS_CONFIG") ?? ".config/purview-build-sdk.json");
	const config = readConfig(configPath);

	const agents = option("agents") ?? environment("PURVIEW_SKILLS_AGENTS");
	const candidates = environment("PURVIEW_SKILLS_RUNNER_CANDIDATES");
	const lockFile = option("lock-file") ?? environment("PURVIEW_SKILLS_LOCK_FILE") ?? configString(config, "LockFile", configPath);

	return {
		root,
		configPath,
		agents: agents !== undefined ? splitList(agents) : (configList(config, "Agents", configPath) ?? []),
		runner: option("runner") ?? environment("PURVIEW_SKILLS_RUNNER") ?? configString(config, "Runner", configPath) ?? "bun",
		runnerCandidates:
			candidates !== undefined
				? splitList(candidates)
				: (configList(config, "RunnerCandidates", configPath) ?? ["bun", "npx", "bunx", "pnpm", "yarn", "deno"]),
		packageSpec:
			option("package") ?? environment("PURVIEW_SKILLS_PACKAGE") ?? configString(config, "Package", configPath) ?? "skills@1",
		command: option("command") ?? environment("PURVIEW_SKILLS_COMMAND") ?? configString(config, "Command", configPath) ?? "",
		lockFile: resolve(root, lockFile ?? "skills-lock.json"),
		dryRun:
			values["dry-run"] === true ||
			(parseBoolean(environment("PURVIEW_SKILLS_DRY_RUN")) ?? configBoolean(config, "DryRun", configPath) ?? false),
		agentDirectories: [...defaultAgentDirectories, ...configAgentDirectories(config, configPath)],
	};
}

function launcherFor(runner: string, spec: string): string {
	const launcher = launchers[runner];
	return launcher === undefined ? `${runner} ${spec}` : launcher(spec);
}

// PATH is probed directly rather than by starting a version check: no process, nothing to download,
// nothing to hang. PATHEXT is honoured on Windows, where these commands are .cmd shims.
function findOnPath(names: string[]): { name: string; path: string } | undefined {
	const windows = process.platform === "win32";
	const extensions = windows ? ["", ...(environment("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").split(";").filter(Boolean)] : [""];
	const directories = (process.env["PATH"] ?? "")
		.split(delimiter)
		.map((entry: string) => entry.trim().replace(/^"|"$/g, ""))
		.filter((entry: string) => entry.length > 0);

	for (const name of names) {
		for (const directory of directories) {
			for (const extension of extensions) {
				const candidate = join(directory, name + extension);
				try {
					if (statSync(candidate).isFile()) return { name, path: candidate };
				} catch {
					// Not here; a malformed PATH entry must not stop the probe either.
				}
			}
		}
	}

	return undefined;
}

// 'skills' already on PATH is tried first because it needs no download, then the configured runner,
// then the other candidates - so a machine with Node but no Bun still installs.
function resolveLauncher(settings: Settings): string {
	if (settings.command !== "") return settings.command;

	const found = findOnPath(unique(["skills", settings.runner, ...settings.runnerCandidates]));
	if (found !== undefined) {
		const launcher = launcherFor(found.name, settings.packageSpec);
		info(`Using the skills CLI via '${launcher}' (${found.path}).`);
		return launcher;
	}

	// A dry run runs nothing, so it works on a machine with no runner at all.
	if (settings.dryRun) return launcherFor(settings.runner, settings.packageSpec);

	throw new SkillsError(
		`PRSKL0004 Cannot run the skills CLI: none of ${unique(["skills", settings.runner, ...settings.runnerCandidates]).join(", ")} was found on PATH. ` +
			"Install Bun (which provides 'bun x') or Node.js (which provides npx), or any of pnpm, yarn or deno, " +
			"or install the CLI globally with 'npm install -g skills'. Choose a runner with --runner or Skills.Runner; " +
			"replace the launcher entirely with --command or Skills.Command.",
	);
}

function agentArguments(settings: Settings): string {
	// The CLI does not split a comma-separated --agent, so each agent gets its own flag.
	return settings.agents.map((agent: string) => ` --agent ${quote(agent)}`).join("");
}

function execute(settings: Settings, commandLine: string): number {
	info(settings.dryRun ? `Agent skills (dry run): ${commandLine}` : `Agent skills: ${commandLine}`);
	if (settings.dryRun) return 0;

	const result = spawnSync(commandLine, { cwd: settings.root, shell: true, stdio: "inherit" });
	if (result.error !== undefined) {
		console.error(result.error.message);
		return 1;
	}

	return result.status ?? 1;
}

function addSource(settings: Settings, source: string): void {
	const launcher = resolveLauncher(settings);
	// '-y' keeps 'add' non-interactive. '*' is every skill the source has, and a re-run is safe: the
	// CLI reinstalls each skill it is given, replacing the installed copy.
	const exitCode = execute(settings, `${launcher} add ${quote(source)} --skill "*"${agentArguments(settings)} -y`);
	if (exitCode !== 0) report("PRSKL0008", `Agent skills: adding '${source}' failed with exit code ${exitCode}.`);
}

function install(settings: Settings, source: string | undefined): void {
	if (source !== undefined) {
		addSource(settings, source);
		return;
	}

	if (!existsSync(settings.lockFile))
		throw new SkillsError(
			`PRSKL0007 Nothing to install: no source was given, and '${settings.lockFile}' does not exist to restore from. ` +
				"Install from a source first, for example: just skills-add purview-dev/build-sdk",
		);

	const exitCode = execute(settings, `${resolveLauncher(settings)} experimental_install`);
	if (exitCode !== 0) report("PRSKL0008", `Agent skills: restoring '${settings.lockFile}' failed with exit code ${exitCode}.`);
}

function readLockSources(lockFile: string): string[] {
	let document: unknown;
	try {
		document = JSON.parse(readFileSync(lockFile, "utf8"));
	} catch (exception) {
		throw new SkillsError(`PRSKL0007 Cannot read '${lockFile}': ${(exception as Error).message}`);
	}

	const skills = (document as { skills?: Record<string, { source?: unknown }> } | null)?.skills ?? {};
	const sources = Object.values(skills)
		.map((entry: { source?: unknown }) => (typeof entry?.source === "string" ? entry.source.trim() : ""))
		.filter((source: string) => source.length > 0);

	return unique(sources).sort((left: string, right: string) => left.toLowerCase().localeCompare(right.toLowerCase()));
}

// Update is 'add', never 'skills update'. 'skills add' reinstalls every skill it selects, so one
// re-add of each source, with every skill it has, both refreshes what is installed and brings in
// anything the source has gained. The lockfile's own skill names are deliberately not reused: naming
// them would leave out a skill added since. Every source is attempted even when one fails.
function update(settings: Settings): void {
	if (!existsSync(settings.lockFile))
		throw new SkillsError(
			`PRSKL0007 Cannot update agent skills: '${settings.lockFile}' does not exist. ` +
				"Update re-adds the sources the skills CLI recorded there; install skills first with: just skills-add <source>",
		);

	const sources = readLockSources(settings.lockFile);
	if (sources.length === 0) {
		info(`Agent skills: no sources recorded in ${settings.lockFile}.`);
		return;
	}

	const launcher = resolveLauncher(settings);
	const failures: string[] = [];
	for (const source of sources) {
		const exitCode = execute(settings, `${launcher} add ${quote(source)} --skill "*"${agentArguments(settings)} -y`);
		if (exitCode !== 0) {
			report("PRSKL0008", `Agent skills: re-adding '${source}' failed with exit code ${exitCode}.`);
			failures.push(source);
		}
	}

	if (settings.dryRun) info(`Agent skills: dry run, ${sources.length} source(s), nothing changed.`);
	else if (failures.length === 0) info(`Agent skills: ${sources.length} source(s) refreshed.`);
}

function pathExists(path: string): boolean {
	// lstat does not follow a link, so a dangling one still counts as present.
	try {
		return lstatSync(path, { throwIfNoEntry: false }) !== undefined;
	} catch {
		return false;
	}
}

// Removes a link and never its target: unlink on Unix, and on Windows the directory form for a
// directory symlink or junction.
function deleteLink(path: string): void {
	try {
		unlinkSync(path);
	} catch {
		rmdirSync(path);
	}
}

// When agents are selected, the only agent skill directories allowed are theirs, plus '.agents/skills',
// the CLI's canonical store that every agent directory links into. With fix set, the CLI's links are
// removed - a link only, never its target - and folders left empty go too. A real file is not provably
// the CLI's, so it is kept and reported.
function enforceDirectories(settings: Settings, fix: boolean): void {
	if (settings.agents.length === 0) {
		info(
			"Agent skills: no agents are selected, so the skills CLI auto-detects agents and every agent skill directory is expected. " +
				`Set Skills.Agents in ${relative(settings.root, settings.configPath).replace(/\\/g, "/")} (or pass --agents) to enforce a layout.`,
		);
		return;
	}

	// '*' is every agent, so every directory is allowed.
	if (settings.agents.includes("*")) return;

	const windows = process.platform === "win32";
	const key = (path: string): string => (windows ? path.toLowerCase() : path);
	const selected = settings.agents.map((agent: string) => agent.toLowerCase());
	const root = resolve(settings.root);
	const toRelative = (path: string): string => relative(root, path).replace(/\\/g, "/");

	const directories = settings.agentDirectories
		.map((entry: AgentDirectory) => ({ path: entry.path.replace(/\\/g, "/").replace(/^\/+|\/+$/g, ""), agents: entry.agents }))
		.filter((entry: AgentDirectory) => entry.path.length > 0);

	const known: Set<string> = new Set(directories.flatMap((entry: AgentDirectory) => entry.agents.map((agent: string) => agent.toLowerCase())));
	for (const agent of settings.agents.filter((agent: string) => !known.has(agent.toLowerCase())))
		warn(
			"PRSKL0016",
			`Agent skills: '${agent}' is selected but has no known skills directory, so none is allowed for it. ` +
				`Add it to Skills.AgentDirectories in ${toRelative(settings.configPath)} if the skills CLI supports it.`,
		);

	const allowed: Set<string> = new Set([key(".agents/skills")]);
	for (const entry of directories) {
		if (entry.agents.some((agent: string) => selected.includes(agent.toLowerCase()))) allowed.add(key(entry.path));
	}

	const protectedPaths: Set<string> = new Set([key(resolve(root, "skills"))]);

	const grouped: Map<string, AgentDirectory> = new Map();
	for (const entry of directories.filter((candidate: AgentDirectory) => !allowed.has(key(candidate.path)))) {
		const existing = grouped.get(key(entry.path));
		if (existing === undefined) grouped.set(key(entry.path), { path: entry.path, agents: [...entry.agents] });
		else existing.agents.push(...entry.agents);
	}

	const removed: string[] = [];
	let violations = 0;
	const kept: string[] = [];

	const prune = (path: string): void => {
		const stats = lstatSync(path);
		if (stats.isSymbolicLink()) {
			deleteLink(path);
			removed.push(toRelative(path));
			return;
		}

		if (!stats.isDirectory()) {
			kept.push(toRelative(path));
			return;
		}

		for (const child of readdirSync(path).sort()) prune(join(path, child));

		if (readdirSync(path).length === 0) {
			rmdirSync(path);
			removed.push(toRelative(path));
		}
	};

	const ordered = [...grouped.values()].sort((left: AgentDirectory, right: AgentDirectory) =>
		left.path < right.path ? -1 : left.path > right.path ? 1 : 0,
	);

	for (const directory of ordered) {
		const full = resolve(root, directory.path);
		if (protectedPaths.has(key(full)) || !pathExists(full)) continue;

		kept.length = 0;

		// A dry run changes nothing, so it names what fix would clean up and leaves the outcome open.
		if (fix && settings.dryRun) {
			info(
				`Agent skills (dry run): would remove the skills CLI's links from '${directory.path}', ` +
					`the skills directory for ${directory.agents.join(", ")}, which the selected agents do not include.`,
			);
			continue;
		}

		if (fix) {
			prune(full);

			// A folder that only ever held the agent's skills is left empty; remove it too, but never
			// climb to the repository root.
			let parent = dirname(full);
			while (
				!pathExists(full) &&
				parent.length > root.length &&
				existsSync(parent) &&
				readdirSync(parent).length === 0
			) {
				rmdirSync(parent);
				removed.push(toRelative(parent));
				parent = dirname(parent);
			}

			if (!pathExists(full)) continue;
		}

		violations++;
		const agents = directory.agents.join(", ");
		if (fix) {
			// The CLI copies rather than links for some agent selections, so a kept folder can hold every
			// file of every skill: name enough to recognise, then count the rest.
			const sample = kept.slice(0, 5).join(", ") + (kept.length > 5 ? ` and ${kept.length - 5} more` : "");
			report(
				"PRSKL0015",
				`Agent skills: '${directory.path}' is the skills directory for ${agents}, which the selected agents (${settings.agents.join(", ")}) do not include, ` +
					`and still holds content the skills CLI did not create: ${sample}. Remove it by hand.`,
			);
		} else {
			report(
				"PRSKL0015",
				`Agent skills: '${directory.path}' is the skills directory for ${agents}, which the selected agents (${settings.agents.join(", ")}) do not include. ` +
					"Remove it with: just skills-fix",
			);
		}
	}

	for (const path of removed) info(`Agent skills: removed ${path}`);

	if (fix && settings.dryRun) info("Agent skills: dry run, nothing removed.");
	else if (violations === 0)
		info(
			fix
				? `Agent skills: ${removed.length} path(s) removed; only the directories for ${settings.agents.join(", ")} exist.`
				: `Agent skills: only the directories for ${settings.agents.join(", ")} exist.`,
		);
}

function main(argv: string[]): number {
	const { values, positionals } = parseArgs({
		args: argv,
		allowPositionals: true,
		options: {
			root: { type: "string" },
			config: { type: "string" },
			agents: { type: "string" },
			runner: { type: "string" },
			command: { type: "string" },
			package: { type: "string" },
			"lock-file": { type: "string" },
			"dry-run": { type: "boolean" },
			help: { type: "boolean", short: "h" },
		},
	});

	const [command, ...rest] = positionals;
	if (values.help === true || command === undefined || command === "help") {
		info(usage);
		return command === undefined && values.help !== true ? 1 : 0;
	}

	const settings = resolveSettings(values as Record<string, string | boolean | undefined>);

	switch (command) {
		case "add":
			if (rest[0] === undefined) throw new SkillsError("PRSKL0007 'add' needs a source, for example: add purview-dev/build-sdk");
			addSource(settings, rest[0]);
			break;
		case "install":
			install(settings, rest[0]);
			break;
		case "update":
			update(settings);
			break;
		case "check":
			enforceDirectories(settings, false);
			break;
		case "fix":
			enforceDirectories(settings, true);
			break;
		default:
			console.error(`Unknown command '${command}'.`);
			info(usage);
			return 1;
	}

	return failed ? 1 : 0;
}

try {
	process.exitCode = main(process.argv.slice(2));
} catch (exception) {
	if (exception instanceof SkillsError) {
		const match = /^(PRSKL\d{4}) ([\s\S]*)$/.exec(exception.message);
		if (match === null) report("PRSKL0018", exception.message);
		else report(match[1] ?? "", match[2] ?? "");
	} else {
		console.error((exception as Error).message);
	}

	process.exitCode = 1;
}
