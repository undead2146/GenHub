# GitHubRepositoryDiscoveryService Requirements

## Overview
The `GitHubRepositoryDiscoveryService` implements `IGitHubRepositoryDiscoveryService` to automatically discover and catalog Command & Conquer: Generals Zero Hour **fork repositories** that provide **downloadable content** to users.

## CRITICAL FORK REQUIREMENT

**MANDATORY**: Only fork repositories are valid. Original/base repositories are excluded.

### Fork Validation Rules
1. **Repository MUST be a fork** (`repo.IsFork == true`)
2. **Non-forks are automatically rejected** regardless of content quality
3. **Base repositories are excluded** (electronicarts/CnC_Generals_Zero_Hour, TheSuperHackers/GeneralsGameCode)
4. **Search queries MUST include `fork:true`** to prevent non-forks from being discovered

### Fork Detection Process
1. **Fork Discovery**: Query fork networks of primary repositories via GitHub API
2. **Search-Based Discovery**: Use GitHub search with `fork:true` qualifier for comprehensive coverage
3. **Validation**: Verify each discovered repository has `IsFork == true`
4. **Rejection**: Log and reject any non-fork repositories with clear reasoning

## Primary Repositories (Base - NOT included in results)

### Official Repository
- **Repository**: `electronicarts/CnC_Generals_Zero_Hour`
- **Description**: Contains the original game files and serves as the base repository
- **Usage**: Fork discovery starting point only - NOT included in results

### Community Repository
- **Repository**: `TheSuperHackers/GeneralsGameCode`
- **Description**: Primary community fork with active development
- **Usage**: Fork discovery starting point only - NOT included in results

## MANDATORY Content Requirements

**CRITICAL**: A fork repository is ONLY valid if it has **at least one** of the following:

### GitHub Releases (Preferred)
- **Must have published releases** with downloadable assets (.zip, .exe, .msi files)
- Releases provide direct downloads for users
- Empty releases without assets DO NOT count
- Draft releases DO NOT count

### GitHub Workflows (Alternative)
- **Must have successful workflow runs** that produce artifacts
- Workflows indicate active build automation
- Failed/cancelled workflows DO NOT count
- Repositories with workflow files but no runs DO NOT count

### Absolute Exclusions
- **NO RELEASES + NO WORKFLOWS = INVALID REPOSITORY**
- **NOT A FORK = INVALID REPOSITORY**
- Repository size, stars, forks, activity are IRRELEVANT without releases/workflows
- Large codebases without releases/workflows are useless to end users
- Community engagement metrics are meaningless without downloadable content

## Discovery Criteria

### Fork Detection
- Discover all forks of both primary repositories through GitHub API
- Use GitHub search with `fork:true` qualifier for comprehensive discovery
- Traverse fork networks to find active development branches
- Check each fork for releases and workflows

### Validation Process
1. **Fork Check**: Verify `repository.IsFork == true` - reject if false
2. **Release Check**: Query `/repos/{owner}/{repo}/releases` endpoint
   - Must have at least 1 published release with assets
   - OR
3. **Workflow Check**: Query `/repos/{owner}/{repo}/actions/runs` endpoint  
   - Must have at least 1 successful workflow run
   - Check for artifacts in successful runs

### Secondary Filters (Only after fork + releases/workflows confirmed)
- Exclude private repositories
- Exclude archived repositories
- Exclude disabled repositories
- Prefer repositories with recent activity

## Performance Requirements

### Execution Time
- **Target**: Complete discovery within 90 seconds
- **Maximum**: 120 seconds before timeout
- **Optimization**: Use batch processing and concurrent validation

### API Efficiency
- Use batch processing for validation (10 repositories per batch)
- Implement intelligent rate limiting (200ms between batches)
- Minimize redundant API calls through caching

## Service Behavior

### Discovery Process
1. Query primary repositories for fork networks (base repos not added to results)
2. Use GitHub search with `fork:true` to find additional forks
3. **MANDATORY**: Check each repository is a fork (`IsFork == true`)
4. **MANDATORY**: Check each fork for releases/workflows
5. **REJECT** any repository that is not a fork OR lacks releases/workflows
6. Apply secondary filtering to remaining valid fork repositories
7. Cache results and return validated fork repositories

### Validation Enforcement
- **Zero tolerance** for non-fork repositories
- **Zero tolerance** for repositories without releases/workflows
- Log detailed rejection reasons for debugging
- Provide clear metrics on how many repositories were rejected for each reason

### Error Handling
- Continue discovery if individual API calls fail
- Default to REJECTION if fork status cannot be verified
- Default to REJECTION if releases/workflows cannot be verified
- Log all validation failures with specific reasons

## Expected Outcomes
- Users can immediately download releases or artifacts from ALL discovered repositories
- Every repository in the results is a fork with actual downloadable content
- No original/base repositories in results (only their forks)
- No "dead" fork repositories that are just source code without deliverables
- Clear distinction between development forks and distribution forks
- Fast discovery process (<90 seconds) with comprehensive coverage

## Marker Repositories (Must Be Found)
- `jmarshall2323/CnC_Generals_Zero_Hour` - Fork of EA repository
- `x64-dev/GeneralsGameCode_GeneralsOnline` - Fork of community repository

These marker repositories validate discovery service effectiveness and must always be found if they exist and meet requirements.
