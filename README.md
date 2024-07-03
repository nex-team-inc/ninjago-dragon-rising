# Starter

The starter project with motion control.

## Change Project Name

You can easily use this project to start your own project.
You only need to:

- Change the root folder name
- Change the unity folder name
- Change the `Product Name` in Unity project settings (Player Tab).

## What's Included

- **Pre-installed libraries**
  - MDK & OpenCV
  - Nex Utils like NPUI Kit, Debug Settings, Keyboard Navigation etc.
  - Common Utils like DOTween, MMFeedback, MixPanel, Recorder etc.

- **Nex Playground Configuration**
  - Project Settings following the Playground Adaptation Guideline.
  - Term Of Use / Privacy Policy content.
  - Nex splash screen

- **Infrastructures**
  - UI management system (ViewManager)
  - Singletons (Bgm, SFX, Analytics, PlayerDataManager, etc)
  - Detection & setup system.

- **ExampleGameScene**
  - How to use PlayersManager to start body detection.
  - How to use PreviewsManager to display camera view.
  - How to use SetupStateManager to manage setup states.
  - How to use Singletons.

## How to fork this repository

First you need to create a new repo on github. Suppose your repo is called YOUR_REPO, execute the following commands.

```bash
git clone git@github.com:nex-team-inc/starter.git YOUR_REPO/
cd YOUR_REPO/
git lfs fetch --all
git remote set-url origin YOUR_REPO_URL # Get the repo url from the github repo page.
git push  # Push all git commits and history.
git lfs push origin --all  # Push all lfs objects as well.
```
  
## What's Next

- Any file in this project is editable. You can customize it to your game logic.

## Others

- This repo requires code reading, if you want to add your own logic, i.e. you need basic engineering knowledge to use this repo.
