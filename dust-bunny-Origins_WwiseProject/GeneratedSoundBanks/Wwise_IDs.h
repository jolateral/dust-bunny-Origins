/////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Audiokinetic Wwise generated include file. Do not edit.
//
/////////////////////////////////////////////////////////////////////////////////////////////////////

#ifndef __WWISE_IDS_H__
#define __WWISE_IDS_H__

#include <AK/SoundEngine/Common/AkTypes.h>

namespace AK
{
    namespace EVENTS
    {
        static const AkUniqueID EXIT_UI = 1041028614U;
        static const AkUniqueID PLAY_AMB_CAT = 1467608791U;
        static const AkUniqueID PLAY_AMB_FOOTSTEP_BAREFOOT = 1437669270U;
        static const AkUniqueID PLAY_AMB_FOOTSTEP_SLIPPER = 2859770665U;
        static const AkUniqueID PLAY_AMB_ROOM_LP = 3112684807U;
        static const AkUniqueID PLAY_MUS_STARTMENU = 1973883363U;
        static const AkUniqueID PLAY_MUS_ZONES = 3793537147U;
        static const AkUniqueID PLAY_SFX_BRIDGE_CREAK_NL = 3975145325U;
        static const AkUniqueID PLAY_SFX_BRIDGE_LAND_NL = 92681520U;
        static const AkUniqueID PLAY_SFX_BUNNYABSORB_NL = 1567933058U;
        static const AkUniqueID PLAY_SFX_BUNNYHOP = 3966847515U;
        static const AkUniqueID PLAY_SFX_BUNNYIMPACT_NL = 4038925685U;
        static const AkUniqueID PLAY_SFX_BUNNYJUMP_NL = 674581991U;
        static const AkUniqueID PLAY_SFX_BUNNYLAND_NL = 2787500338U;
        static const AkUniqueID PLAY_SFX_BUNNYROLL_NL = 4212669292U;
        static const AkUniqueID PLAY_SFX_CAR_MOVE_NL = 4207159517U;
        static const AkUniqueID PLAY_SFX_ENDSEQ = 1833036738U;
        static const AkUniqueID PLAY_SFX_MEMORY_DRAWING_NL_001 = 1142362393U;
        static const AkUniqueID PLAY_SFX_MEMORY_DRAWING_NL_002 = 1142362394U;
        static const AkUniqueID PLAY_SFX_MEMORY_DRAWING_NL_003 = 1142362395U;
        static const AkUniqueID PLAY_SFX_MEMORY_DRAWING_NL_004 = 1142362396U;
        static const AkUniqueID PLAY_SFX_MEMORY_DRAWING_NL_005 = 1142362397U;
        static const AkUniqueID PLAY_SFX_UI_SELECT_NL = 4289076888U;
        static const AkUniqueID PLAY_SFX_WOODBLOCK_IMPACT_WOOD = 841695155U;
        static const AkUniqueID STOP_MUS_STARTMENU = 200855961U;
        static const AkUniqueID STOP_MUS_ZONES = 75519133U;
    } // namespace EVENTS

    namespace STATES
    {
        namespace PLAYER_STATE
        {
            static const AkUniqueID GROUP = 4071417932U;

            namespace STATE
            {
                static const AkUniqueID GLIDING = 4176812997U;
                static const AkUniqueID MEMORY = 3509424520U;
                static const AkUniqueID NONE = 748895195U;
                static const AkUniqueID PAUSE = 3092587493U;
            } // namespace STATE
        } // namespace PLAYER_STATE

        namespace ZONES
        {
            static const AkUniqueID GROUP = 831766718U;

            namespace STATE
            {
                static const AkUniqueID NONE = 748895195U;
                static const AkUniqueID ZONE1 = 831766780U;
                static const AkUniqueID ZONE2 = 831766783U;
            } // namespace STATE
        } // namespace ZONES

    } // namespace STATES

    namespace SWITCHES
    {
        namespace BUNNY_STEP
        {
            static const AkUniqueID GROUP = 722740280U;

            namespace SWITCH
            {
                static const AkUniqueID NOT_GROUNDED = 3128490829U;
            } // namespace SWITCH
        } // namespace BUNNY_STEP

    } // namespace SWITCHES

    namespace GAME_PARAMETERS
    {
        static const AkUniqueID BUNNY_SIZE = 404510017U;
        static const AkUniqueID GROUNDED = 2907122923U;
        static const AkUniqueID VELOCITY = 3519441192U;
    } // namespace GAME_PARAMETERS

    namespace BUSSES
    {
        static const AkUniqueID MAIN_AUDIO_BUS = 2246998526U;
    } // namespace BUSSES

    namespace AUX_BUSSES
    {
        static const AkUniqueID ZONE1 = 831766780U;
        static const AkUniqueID ZONE2 = 831766783U;
    } // namespace AUX_BUSSES

    namespace AUDIO_DEVICES
    {
        static const AkUniqueID NO_OUTPUT = 2317455096U;
        static const AkUniqueID SYSTEM = 3859886410U;
    } // namespace AUDIO_DEVICES

}// namespace AK

#endif // __WWISE_IDS_H__
