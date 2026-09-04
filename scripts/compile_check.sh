#!/bin/bash
cd "$(dirname "$0")/.."
cd scripts/decision_system_tests || exit 1
for MODE in ENABLE_LEGACY_INPUT_MANAGER ENABLE_INPUT_SYSTEM; do
  echo "=== $MODE ==="
  mcs -langversion:latest -target:library -define:"$MODE" -out:"FullCheck_$MODE.dll" \
    TestJson.cs ../unity-stub/UnityStub.cs \
    ../../Assets/_Project/Scripts/Core/*.cs \
    ../../Assets/_Project/Scripts/Narrative/*.cs \
    ../../Assets/_Project/Scripts/Narrative/Content/*.cs \
    ../../Assets/_Project/Scripts/Narrative/Abilities/*.cs \
    ../../Assets/_Project/Scripts/Gameplay/*.cs \
    ../../Assets/_Project/Scripts/Gameplay/Interaction/*.cs \
    ../../Assets/_Project/Scripts/Gameplay/Abilities/*.cs \
    ../../Assets/_Project/Scripts/Gameplay/WorldState/*.cs \
    ../../Assets/_Project/Scripts/Gameplay/World/*.cs \
    ../../Assets/_Project/Scripts/Gameplay/Combat/*.cs \
    ../../Assets/_Project/Scripts/Gameplay/NPC/*.cs \
    ../../Assets/_Project/Scripts/UI/*.cs \
    ../../Assets/Game/Scripts/FirstLocationBootstrap.cs \
    ../../Assets/Game/Scripts/ThirdPersonCameraController.cs 2>&1 \
    | grep -vE "CS0219|CS0414|CS0169|warning CS" | head -12
  echo "exit=${PIPESTATUS[0]}"
done
