﻿using Google.Protobuf;
using POGOProtos.Data.Player;
using POGOProtos.Enums;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Requests.Messages;
using POGOProtos.Networking.Responses;
using PokemonGoGUI.Extensions;
using PokemonGoGUI.GoManager.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PokemonGoGUI.GoManager
{
    public partial class Manager
    {
        public async Task<MethodResult> MarkStartUpTutorialsComplete(bool forceAvatarUpdate)
        {
            MethodResult reauthResult = await CheckReauthentication();

            if (!reauthResult.Success)
            {
                return reauthResult;
            }

            bool success = true;

            if (PlayerData == null) {
                return new MethodResult{ Success = false };
            }


            var completedTutorials = PlayerData.TutorialState;

            if (!completedTutorials.Contains(TutorialState.LegalScreen))
            {
                await MarkTutorialsComplete ( new[] {TutorialState.LegalScreen});
                await GetPlayer();
            }
            await Task.Delay(CalculateDelay(UserSettings.GeneralDelay, UserSettings.GeneralDelayRandom));

            if (!completedTutorials.Contains(TutorialState.AvatarSelection) || forceAvatarUpdate)
            {
                await SetPlayerAvatar();
                await Task.Delay(CalculateDelay(UserSettings.GeneralDelay, UserSettings.GeneralDelayRandom));
                await MarkTutorialsComplete ( new[] {TutorialState.AvatarSelection});
                await GetPlayerProfile();
            }
            else
            {
                LogCaller(new LoggerEventArgs("Avatar already set", LoggerTypes.Info));
            }

            if (!completedTutorials.Contains(TutorialState.PokemonCapture))
            {
                // TODO:
                /*
                getDownloadURLs([
                this.state.assets.getFullIdFromId('1a3c2816-65fa-4b97-90eb-0b301c064b7a'),
                this.state.assets.getFullIdFromId('aa8f7687-a022-4773-b900-3a8c170e9aea'),
                this.state.assets.getFullIdFromId('e89109b0-9a54-40fe-8431-12f7826c8194'),
                ]);*/
                
                var pokemon = new List<PokemonId>
                {
                    PokemonId.Charmander,
                    PokemonId.Bulbasaur,
                    PokemonId.Squirtle,
                    PokemonId.Pikachu
                };
    
                PokemonId chosenPokemon = PokemonId.Pikachu;
    
                lock (_rand)
                {
                    chosenPokemon = pokemon[_rand.Next(0, pokemon.Count)];
                }
                MethodResult result = await CompleteEncounterTutorial(chosenPokemon);

                if (!result.Success)
                {
                    success = false;
                }
                await GetPlayer(false);

                await Task.Delay(CalculateDelay(UserSettings.GeneralDelay, UserSettings.GeneralDelayRandom));
            }
            if (!completedTutorials.Contains(TutorialState.NameSelection))
            {
                await Task.Delay(CalculateDelay(UserSettings.GeneralDelay, UserSettings.GeneralDelayRandom));
                await ClaimCodename(UserSettings.Username);
                await GetPlayer(false);
                await MarkTutorialsComplete ( new[] {TutorialState.NameSelection},false);
            }

            if (!completedTutorials.Contains(TutorialState.FirstTimeExperienceComplete))
            {
                await Task.Delay(CalculateDelay(UserSettings.GeneralDelay, UserSettings.GeneralDelayRandom));
                await MarkTutorialsComplete ( new[] {TutorialState.FirstTimeExperienceComplete},false);
            }

            if (success)
            {
                LogCaller(new LoggerEventArgs("Finished tutorial", LoggerTypes.Debug));
            }
            else
            {
                LogCaller(new LoggerEventArgs("Error occured when completing tutorial", LoggerTypes.Warning));
            }

            return new MethodResult()
            {
                Success = true
            };
        }

        public async Task<MethodResult> MarkTutorialsComplete(TutorialState[] tutorials, bool nobuddy = true, bool noinbox= true)
        {
            try
            {
                var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
                {
                    RequestType = RequestType.MarkTutorialComplete,
                    RequestMessage = new MarkTutorialCompleteMessage
                    {
                        SendMarketingEmails = false,
                        SendPushNotifications = false,
                        TutorialsCompleted = { tutorials }
                    }.ToByteString()
                },true,nobuddy,noinbox);

                MarkTutorialCompleteResponse markTutorialCompleteResponse = null;

                markTutorialCompleteResponse = MarkTutorialCompleteResponse.Parser.ParseFrom(response);
                LogCaller(new LoggerEventArgs("Tutorial completion request wasn't successful", LoggerTypes.Success));

                _client.ClientSession.Player.Data = markTutorialCompleteResponse.PlayerData;

                return new MethodResult
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                LogCaller(new LoggerEventArgs("Failed to mark tutorials as complete", LoggerTypes.Exception, ex));

                return new MethodResult();
            }
        }

        public async Task<MethodResult> CompleteEncounterTutorial(PokemonId pokemon)
        {
            try
            {
                var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
                {
                    RequestType = RequestType.EncounterTutorialComplete,
                    RequestMessage = new EncounterTutorialCompleteMessage
                    {
                        PokemonId = pokemon
                    }.ToByteString()
                });

                EncounterTutorialCompleteResponse encounterTutorialCompleteResponse = null;

                encounterTutorialCompleteResponse = EncounterTutorialCompleteResponse.Parser.ParseFrom(response);
                LogCaller(new LoggerEventArgs(String.Format("Caught a {0}", pokemon), LoggerTypes.Success));

                return new MethodResult
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                LogCaller(new LoggerEventArgs("EncounterTutorialCompleteResponse parsing failed because response was empty", LoggerTypes.Exception, ex));

                return new MethodResult();
            }
        }

        public async Task<MethodResult> SetPlayerAvatar()
        {
            try
            {
                var avatar = new PlayerAvatar();

                var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
                {
                    RequestType = RequestType.SetAvatar,
                    RequestMessage = new SetAvatarMessage
                    {
                        PlayerAvatar = avatar
                    }.ToByteString()
                },true,true,true);

                SetAvatarResponse setAvatarResponse = null;

                setAvatarResponse = SetAvatarResponse.Parser.ParseFrom(response);
                LogCaller(new LoggerEventArgs("Avatar set to defaults", LoggerTypes.Success));


                return new MethodResult
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                LogCaller(new LoggerEventArgs("Failed to set avatar", LoggerTypes.Exception, ex));

                return new MethodResult();
            }
        }

        public async Task<MethodResult> ListAvatarCustomizations()
        {
            try
            {
                var avatar = new PlayerAvatar();

                var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
                {
                    RequestType = RequestType.ListAvatarCustomizations,
                    RequestMessage = new ListAvatarCustomizationsMessage
                    {
                       Filters  = { Filter.Default}
                    }.ToByteString()
                },true,true,true);


                var parsedResponse = ListAvatarCustomizationsResponse.Parser.ParseFrom(response);
                LogCaller(new LoggerEventArgs("ListAvatarCustomizations set to defaults", LoggerTypes.Success));


                return new MethodResult
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                LogCaller(new LoggerEventArgs("Failed to set avatar", LoggerTypes.Exception, ex));

                return new MethodResult();
            }
        }

        public async Task<MethodResult> SetAvatarItemAsViewed()
        {
            try
            {
                var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
                {
                    RequestType = RequestType.SetAvatarItemAsViewed,
                    RequestMessage = new SetAvatarItemAsViewedMessage
                    {
                        //TODO: get avatarids 
                        //AvatarTemplateId = { }
                    }.ToByteString()
                });

                SetAvatarItemAsViewedResponse setAvatarItemAsViewedResponse = null;

                setAvatarItemAsViewedResponse = SetAvatarItemAsViewedResponse.Parser.ParseFrom(response);
                LogCaller(new LoggerEventArgs("Set avatar item as viewed", LoggerTypes.Success));

                return new MethodResult
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                LogCaller(new LoggerEventArgs("SetAvatarItemAsViewedResponse parsing failed because response was empty", LoggerTypes.Exception, ex));

                return new MethodResult();
            }
        }
        public async Task<MethodResult> ClaimCodename(string username)
        {
            try
            {
                var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
                {
                    RequestType = RequestType.ClaimCodename,
                    RequestMessage = new ClaimCodenameMessage
                    {
                        Codename = username,
                        Force  = false
                    }.ToByteString()
                },true, false, true);

                var parsedResponse = ClaimCodenameResponse.Parser.ParseFrom(response);
                LogCaller(new LoggerEventArgs("Set avatar item as viewed", LoggerTypes.Success));

                return new MethodResult
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                LogCaller(new LoggerEventArgs("ClaimCodenameResponse parsing failed because response was empty", LoggerTypes.Exception, ex));

                return new MethodResult();
            }
        }
    }
}
