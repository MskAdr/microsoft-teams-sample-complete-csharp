﻿using Autofac;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Teams;
using Microsoft.Bot.Connector.Teams.Models;
using Microsoft.Teams.TemplateBotCSharp.Properties;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Teams.TemplateBotCSharp.Utility
{
    /// <summary>
    /// Get the locale from incoming activity payload and handle compose extension methods
    /// </summary>
    public static class TemplateUtility
    {
        public static string BotId = ConfigurationManager.AppSettings["MicrosoftAppId"].ToString();
        public static string GetLocale(Activity activity)
        {
            if (activity == null)
            {
                throw new ArgumentNullException(nameof(activity));
            }

            //Get the locale from activity
            if (activity.Entities != null)
            {
                foreach(var entity in activity.Entities)
                {
                    if (string.Equals(entity.Type.ToString().ToLower(), "clientinfo"))
                    {
                        var locale = entity.Properties["locale"];
                        if (locale != null)
                        {
                            return locale.ToString();
                        }
                    }
                }
            }
            return activity.Locale;
        }

        public static ComposeExtensionAttachment CreateComposeExtensionCardsAttachments(WikiHelperSearchResult wikiResult, string selectedType)
        {
            return GetComposeExtensionMainResultAttachment(wikiResult, selectedType).ToComposeExtensionAttachment(GetComposeExtensionPreviewAttachment(wikiResult, selectedType));
        }

        public static ComposeExtensionAttachment CreateComposeExtensionCardsAttachmentsSelectedItem(WikiHelperSearchResult wikiResult, string selectedType)
        {
            return GetComposeExtensionMainResultAttachment(wikiResult, selectedType).ToComposeExtensionAttachment();
        }

        public static Attachment GetComposeExtensionMainResultAttachment(WikiHelperSearchResult wikiResult, string selectedType)
        {
            CardType cardType;
            Attachment cardAttachment = null;

            List<CardImage> images = new List<CardImage>();
            CardImage image = new CardImage(wikiResult.imageUrl);
            images.Add(image);

            if (Enum.TryParse(selectedType, out cardType))
            {
                switch (cardType)
                {
                    case CardType.hero:
                        cardAttachment = new HeroCard()
                        {
                            Title = wikiResult.highlightedTitle,
                            Text = wikiResult.text,
                            Images = images
                        }.ToAttachment();
                        break;
                    case CardType.thumbnail:
                        cardAttachment = new ThumbnailCard()
                        {
                            Title = wikiResult.highlightedTitle,
                            Text = wikiResult.text,
                            Images = images
                        }.ToAttachment();
                        break;
                }
            }

            return cardAttachment;
        }

        public static Attachment GetComposeExtensionPreviewAttachment(WikiHelperSearchResult wikiResult, string selectedType)
        {
            string invokeVal = GetCardActionInvokeValue(wikiResult);
            var tapAction = new CardAction("invoke", value: invokeVal);

            CardType cardType;
            Attachment cardAttachment = null;

            List<CardImage> images = new List<CardImage>();
            CardImage image = new CardImage(wikiResult.imageUrl);
            images.Add(image);

            if (Enum.TryParse(selectedType, out cardType))
            {
                switch (cardType)
                {
                    case CardType.hero:
                        cardAttachment = new HeroCard()
                        {
                            Title = wikiResult.highlightedTitle,
                            Tap = tapAction,
                            Images = images
                        }.ToAttachment();
                        break;
                    case CardType.thumbnail:
                        cardAttachment = new ThumbnailCard()
                        {
                            Title = wikiResult.highlightedTitle,
                            Tap = tapAction,
                            Images = images
                        }.ToAttachment();
                        break;
                }
            }

            return cardAttachment;
        }

        private static string GetCardActionInvokeValue(WikiHelperSearchResult wikiResult)
        {
            string quoted = cleanForJSON(wikiResult.text);
            InvokeValue invokeValue = new InvokeValue(wikiResult.imageUrl, quoted, wikiResult.highlightedTitle);
            JObject json = (JObject)JToken.FromObject(invokeValue);
            return json.ToString();
        }

        /// <summary>
        /// Parse the invoke request json and returned the invoke value
        /// </summary>
        /// <param name="inputString"></param>
        /// <returns></returns>
        public static string ParseInvokeRequestJson(string inputString)
        {
            JObject invokeObjects = JObject.Parse(inputString);

            if (invokeObjects.Count > 0)
            {
                return invokeObjects[Strings.InvokeRequestJsonKey].Value<string>();
            }

            return null;
        }

        public static string cleanForJSON(string s)
        {
            if (s == null || s.Length == 0)
            {
                return "";
            }

            char c = '\0';
            int i;
            int len = s.Length;
            StringBuilder sb = new StringBuilder();
            String t;

            for (i = 0; i < len; i += 1)
            {
                c = s[i];
                switch (c)
                {
                    case '\\':
                    case '"':
                        sb.Append('\\');
                        sb.Append(c);
                        break;
                    case '/':
                        sb.Append('\\');
                        sb.Append(c);
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    default:
                        if (c < ' ')
                        {
                            t = "000" + String.Format("{0} : {1}", "X", c);
                            sb.Append("\\u" + t.Substring(t.Length - 4));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        public static IBotDataStore<BotData> GetBotDataStore(Activity activity)
        {
            using (var scope = DialogModule.BeginLifetimeScope(Conversation.Container, activity))
            {
                // Resolve services from a scope that is a child
                // of the root container.
                var service = scope.Resolve<IBotDataStore<BotData>>();
                return service;
            }
        }

        public static async Task<BotData> GetBotDataObject(IBotDataStore<BotData> botDataStore, IActivity activity)
        {
            IAddress key = new Address(BotId, activity.ChannelId, activity.From.Id, activity.Conversation.Id, activity.ServiceUrl);
            BotData botData = await botDataStore.LoadAsync(key, BotStoreType.BotUserData, CancellationToken.None);
            return botData;
        }

        public static async void SaveBotDataObject(IBotDataStore<BotData> botDataStore, Activity activity, BotData botData)
        {
            IAddress key = new Address(BotId, activity.ChannelId, activity.From.Id, activity.Conversation.Id, activity.ServiceUrl);
            await botDataStore.SaveAsync(key, BotStoreType.BotUserData, botData, CancellationToken.None);
        }
    }

    public class InvokeValue
    {
        public string imageUrl { get; set; }
        public string text { get; set; }
        public string highlightedTitle { get; set; }

        public InvokeValue(string urlValue, string textValue, string highlightedTitleValue)
        {
            imageUrl = urlValue;
            text = textValue;
            highlightedTitle = highlightedTitleValue;
        }
    }
}