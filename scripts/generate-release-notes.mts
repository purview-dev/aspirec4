#!/usr/bin/env bun

import { execFileSync } from 'node:child_process'

const range = process.argv[2]
if (!range) {
  console.error('Usage: bun scripts/generate-release-notes.mts <git-range>')
  process.exit(1)
}

const commitLines = execFileSync('git', ['log', '--format=%B', range], {
  encoding: 'utf8',
})
  .split(/\r?\n/)
  .map(line => line.trim().replace(/^[*-]\s+/, ''))
  .filter(Boolean)

const sections = [
  { heading: 'Features', types: new Set(['feat']) },
  { heading: 'Bug fixes', types: new Set(['fix', 'bug']) },
  { heading: 'Performance', types: new Set(['perf']) },
  { heading: 'Security', types: new Set(['security']) },
  { heading: 'Improvements', types: new Set(['refactor', 'revert']) },
]

const entries = new Map(sections.map(section => [section.heading, [] as string[]]))
const conventionalCommit = /^([a-z]+)(?:\(([^)]+)\))?(!)?:\s*(.+)$/i

for (const line of commitLines) {
  const match = line.match(conventionalCommit)
  if (!match) continue

  const [, rawType, scope, breaking, summary] = match
  const type = rawType.toLowerCase()
  const section = sections.find(candidate => candidate.types.has(type))
  if (!section) continue

  const scopePrefix = scope ? `**${scope}:** ` : ''
  const breakingPrefix = breaking ? '**Breaking:** ' : ''
  const entry = `- ${breakingPrefix}${scopePrefix}${summary}`
  const sectionEntries = entries.get(section.heading)
  if (sectionEntries && !sectionEntries.includes(entry)) sectionEntries.push(entry)
}

const output: string[] = []
for (const section of sections) {
  const sectionEntries = entries.get(section.heading) ?? []
  if (sectionEntries.length === 0) continue

  output.push(`### ${section.heading}`, '', ...sectionEntries, '')
}

if (output.length === 0) {
  output.push('Improvements ongoing.', '')
}

process.stdout.write(output.join('\n'))
