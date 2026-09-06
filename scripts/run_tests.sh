#!/bin/bash
# Headless test runner (all suites). Usage: bash scripts/run_tests.sh
cd "$(dirname "$0")/decision_system_tests" || exit 1
mcs -langversion:latest -define:ENABLE_LEGACY_INPUT_MANAGER -out:FlowTests.exe \
  TestJson.cs FlowTests.cs WorldTests.cs CombatTests.cs MobileExperienceTests.cs CampaignTests.cs LocationTests.cs \
  $( [ -f CampaignContentTests.cs ] && echo CampaignContentTests.cs ) \
  ../unity-stub/UnityStub.cs \
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
  ../../Assets/_Project/Scripts/Gameplay/Input/*.cs \
  ../../Assets/_Project/Scripts/Gameplay/Campaign/*.cs \
  ../../Assets/_Project/Scripts/Gameplay/Locations/*.cs \
  ../../Assets/_Project/Scripts/Gameplay/NPC/*.cs \
  ../../Assets/_Project/Scripts/UI/*.cs \
  ../../Assets/Game/Scripts/FirstLocationBootstrap.cs \
  ../../Assets/Game/Scripts/ThirdPersonCameraController.cs 2>&1 | grep -E "error|Compilation" 
status=${PIPESTATUS[0]}
[ $status -ne 0 ] && { echo "COMPILE FAILED"; exit 1; }
mono FlowTests.exe
