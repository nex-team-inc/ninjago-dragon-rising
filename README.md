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

## Setup Localization

### Secrets Decryption

1. Install `gcloud` CLI according to the [official doc](https://cloud.google.com/sdk/docs/install).

1. Run `gcloud auth login` with your company email address.

1. Run `make` to decrypt Google Sheet service secrets. In case you see errors about permission, ping any engineers in the Music cell / DX team to help.

### Create Localization Table on Google Sheet

1. On Google Drive, Open [Physics Activities -> Music Cell -> Localization](https://drive.google.com/drive/folders/1ZRUBFV-3qLlu0ixPQjyAqSC7_yydLkng)

1. Clone a spreadsheet from "Starter Localization". Name it "<Game Name> Localization".

1. Open the cloned spreadsheet, copy the Spreadsheet ID (the long string between "https://docs.google.com/spreadsheets/d/" and "/edit")

1. On Unity, select the asset `Assets/Localization/LocalizationTables/LocalizationTable.asset`, update the Spreadsheet ID to the new ID.

1. Try Localization -> Google Sheets -> Pull All Google Sheets Extensions to verify the setup. Also test the `GameUIExmaple` scene.

### Notes

- Always make changes on the Google Sheets side and pull the changes onto the Unity side.

- Make sure all fonts used in game has the proper fallback font assets setting. Every UI text should be able to display strings in all supported locales.

- If the UI text has a background, also make sure it could resize properly in different locales.
  - It can be done using a Horizontal Layout Group (with "Control Child Size" option) and a Content Size Fitter on the background component.
  - For examples, check out the play button in the prefab `Assets/Prefabs/Views/WelcomeScreenView.prefab`.

## Setup Remote Config

1. On [Unity Cloud](https://cloud.unity.com/home/), click your profile icon on top right, make sure you are using the "Nex Team Inc." Organization, not "Nex Build Pipeline".

1. Create a project under the "Nex Team Inc." Organization.

1. Go to Projects -> Environments. There should be a "production" environment created by default. Also create a "staing" environment.

1. Go to Products -> Remote Config. Add any remote configs as needed.

1. On Unity, select Edit -> Project Settings -> Services. Select the new project created -> Link Unity project to cloud project.
    1. Also do File -> Save project. Commit the changes in `ProjectSettings/ProjectSettings.asset`.

1. On `Assets/Prefabs/Singletons/CommonSingletons.prefab`, drag and drop the prefab `Assets/Prefabs/Singletons/RemoteConfigManager.prefab` to enable Remote Config.

### Usage

1. On `Assets/Scripts/RemoteConfig/RemoteConfig.cs`, define the fields of the remote config.

    1. If the field is not a primitive type (e.g. a List), also handle the de-serialization.

    1. (Optional) Set the default values under the `RemoteConfigManager` prefab.

1. On Unity Cloud -> Products -> Remote Config, add the key with the same name with the desired value. Click "Publish" for the Remote Config to take effect.

1. Run the game on Editor. Verify the remote config is sucessfully fetched by tracing the log `RemoteConfigManager: Got from ...`

---

## What's Next
- To make sure NBP works, Derek would need Admin role to the repo. Forked repo doesn't inherit Collaborators and Teams settings unfortunately.
- Any file in this project is editable. You can customize it to your game logic.

## Others

- This repo requires code reading, if you want to add your own logic, i.e. you need basic engineering knowledge to use this repo.
