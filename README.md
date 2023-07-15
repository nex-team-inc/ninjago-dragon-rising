# Starter
The starter project with motion control.

https://github.com/nex-team-inc/Starter/assets/30437024/15a11e88-2eb9-48a5-8080-11e60ad747b6

## Change Project Name
You can easily use this project to start your own project.
You only need to:
- Change the root folder name
- Change the unity folder name
- Change the `Product Name` in Unity project settings (Player Tab).

## What's Included
- Installation of OpenCV and MDK.
- GameManager helps you to switch between 1P/2P mode easily.
- OnePlayerGameEngine that manages basic detection, preview frame and game board.
- Pose node smoothing.

## 1P vs 2P
- In GameManager, remove the SinglePlayerGameEngine from the GameEngines list.
- Drag P1GameEngine and P2GameEngine into GameEngines list.
- You can revert to 1P by changing the list back to SinglePlayerGameEngine.

# What's Next
- Any file in this project is editable. You can customise it to your game logic.
- If we don't want the drum demo, just remove the GameBoard from OnePlayerGameEngine prefab and delete the Board folder in Scripts.

# Others
- This repo requires code reading, if you want to add your own logic. Not very non-engineer-friendly.
