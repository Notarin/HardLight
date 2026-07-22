# Branch Structure
## Public Branches
These branches are on the HardLight github and should generally never directly merged or pushed into (neither on a fork or on the official github)
* prod
    * This is the version the game server is running on
    * It should always be only comprised of tested and validated code
    * direct merges into prod are prohibited
    * Merges into prod happen weekly and are only comprised of code from master that has been individually tested by multiple people
    * PRs into prod are from the master branch with an exception for hotfixes
    * Changelogs are read from PRs going into prod
    * Hotfix PRs into prod need to be reviewed for code quality *and* tested by at least one additional reviewer. See [[#Testing Guidelines]]
* master
    * This is the branch the test server is running on (once available)
        * The test server is publicly available for whitelisted players
        * The test server will be updated and restarted daily (if changes exist)
    * PRs into master have to be tested by the developer for stability and functionality and only need to be reviewed by sighting the changed files for code quality. See [[#Testing Guidelines]] and [[#PR Guidelines]]
    * features merged into master can be announced in upcoming-content on the discord
## Fork Branches
These branches are created on forks and are meant for general development. You are free to merge or push into these branches whenever you feel like it. It is recommended to delete the branch after merging it into master. Branches can be restored if needed
* feature
    * A feature branch explicitly adds *new* features to the game or adds/changes capabilities to features that are already in the game.
    * Feature branches are personal branches by developers branching off of the master branch and getting PRd back into master.
* bugfix
    * Bugfix branches are personal branches meant to fix issues that have been found on the master branch
    * They branch off of master and go back into master once the bug is deemed to be fixed
* hotfix
    * Hotfix branches are personal branches that are meant to fix an issue that has been found on the prod branch
    * They branch off of prod and are getting PRd into master *and* prod alike


## General recommendation for branch names
for easier organization on local branches, you can use a forward slash (/) to structure your branches. These branches will be automatically categorized by most git GUIs.
It is also recommended to add issue numbers (if applicable) to the respective branches
Examples:
* feature/12355-new-antag
* bugfix/12355-new-antag-avali
* hotfix/12358-cryosleep-spawn


# PR Guidelines
* PRs need to come from one of the aforementioned fork branches. You cannot PR from your own master or prod branch into HardLight
* All content of a PR should be related to one topic.
* The title and body of a PR need to follow guidelines
    * The title needs to be prefixed with one of the following prefixes
      * **feat**: A new feature
      * **bugfix**: A bug fix
      * **hotfix**: A hot fix
      * **build**: Changes that affect the build system or external dependencies
      * **conf**: Changes to configurations
      * **docs**: Documentation only changes
      * **perf**: A code change that improves performance
      * **refactor**: A code change that neither fixes a bug nor adds a feature
      * **style**: Changes that do not affect the meaning of the code (white-space, formatting, missing semi-colons, etc)
      * **test**: Adding missing tests or correcting existing tests
      * **chore**: Updates and similar tasks, No production code change
    * The title should include the related issue number(s) (if applicable) and be a concise summary of the changes
    * The body should follow the standardized body with every applicable category filled out
* Refactors and cleanups must be in a separate PR
* A PR needs to be classified for size by a maintainer before getting merged
* A PR needs to be tested thoroughly by the developer before submission. See [[#Testing Guidelines]]
* A PR is not considered *submitted* while it is in draft or has the *do not merge* tag
* Keep your PR as small and concise as possible. If you need to implement/port things for your main feature to work, PR these implementations/ports individually first


# Testing Guidelines
* When building and starting the game, no new errors or warnings shall appear in either the server or client console
* Every aspect of your change should be tested
    * YML entity additions/changes
        * Every related entity needs to be spawned, used and observed for functionality
        * The entity needs to be locally saved and loaded on a ship at least once
        * The entity needs to be locally saved and loaded in an apartment at least once
        * child entities of the changed entity need to be tested as well
    * YML recipes
        * Every construction and deconstruction recipe has to be tested at least once
        * If the recipe has alternative construction materials, all combinations should be tested
    * YML game rules
        * The game rule has to be added 5 consecutive times
            * No warnings or errors shall appear in the consoles
    * C# Components
        * a wide variety of entities using the component need to be spawned, used and observed
        * an entity using the component needs to be saved and loaded on a ship
    * New/Ported C# systems
        * The needs are entirely based on the system itself
        * It is required to summarize what kinds of tests have been done
* Unit/Integration tests
    * C# code that adds functionality needs a unit test for every public functionality

