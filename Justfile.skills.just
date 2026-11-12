skills_project := _solution

# Update agent skills: re-add every skills-lock.json source, refreshing installed skills and adding new ones
[group('Agent skills')]
skills-update *args:
    dotnet msbuild "{{ skills_project }}" "-t:UpdatePurviewSkills;FixPurviewSkills" -v:n {{ args }}

# Check that only the skill directories of the agents in PurviewSkillsAgents exist
[group('Agent skills')]
skills-check *args:
    dotnet msbuild "{{ skills_project }}" -t:CheckPurviewSkills -v:n {{ args }}
