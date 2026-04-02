-- SaveManager example Lua trigger
-- Requires MapLoaderFramework (Lua scripting) + SaveManager with SAVEMANAGER_MLF.
-- Demonstrates common save / flag operations callable from a TriggerLua cutscene step.

-- Mark chapter 1 as completed and auto-save
save_manager_set_flag("chapter_01_completed")
save_manager_save()

-- Check a flag before branching dialogue or spawning objects
if save_manager_is_flag_set("met_commander_ross") then
    print("[SaveTrigger] Player already met Commander Ross.")
else
    save_manager_set_flag("met_commander_ross")
    print("[SaveTrigger] Flag 'met_commander_ross' set.")
end

-- Store an arbitrary custom value
save_manager_set_custom("last_choice", "helped_engineer")
