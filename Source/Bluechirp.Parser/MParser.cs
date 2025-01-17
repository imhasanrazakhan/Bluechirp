﻿using System.Collections.Generic;
using System.Net;
using System.Text;
using Bluechirp.Parser.Model;

namespace Bluechirp.Parser
{
    public class MParser
    {
        private const char _SPACE_CHAR = (char) 32;

        private Queue<char> _charQueue;
        private readonly StringBuilder _parseBuffer = new StringBuilder();

        private List<MastoContent> ParseLoop(string Tag)
        {
            string parsedTag = string.Empty;

            List<MastoContent> parsedContent = new List<MastoContent>();

            while (LoopConditionIsTrue(Tag, parsedTag))
            {
                bool inBreakTag = CheckIfTag(parsedTag, ParserConstants.BREAK_TAG);
                char character = _charQueue.Dequeue();

                if (inBreakTag)
                {
                    if (character == ParserConstants.TAG_END_CHARACTER)
                    {
                        TryAddTextToParsedContent(parsedContent, "\n");

                        ClearTag(ref parsedTag);
                    }
                }
                else if (parsedTag != string.Empty)
                {
                    if (CheckIfTag(parsedTag, $"{ParserConstants.LINK_TAG}"))
                    {
                        parsedContent.Add(HandleLinkTag());
                    }
                    else
                    {
                        if (CheckIfTag(parsedTag, $"{ParserConstants.PARAGRAPH_TAG}")) TryAddTextToParsedContent(parsedContent, "\n\n");

                        // Parse through inner content of parsed tag.
                        List<MastoContent> recursiveContent = ParseLoop(parsedTag);
                        parsedContent.AddRange(recursiveContent);
                    }

                    ClearTag(ref parsedTag);
                }
                else
                {
                    // Parse through inner content between open and close tags
                    (bool hasTextToParse, bool hasTagToParse, string text, string tag) textHandlingResult = HandleText(character);

                    if (textHandlingResult.hasTextToParse) TryAddTextToParsedContent(parsedContent, textHandlingResult.text);

                    if (textHandlingResult.hasTagToParse) parsedTag = textHandlingResult.tag;
                }
            }

            if (_parseBuffer.Length > 0) TryAddTextToParsedContent(parsedContent, _parseBuffer.ToString());

            return parsedContent;
        }

        private void ClearTag(ref string ParsedTag)
        {
            ParsedTag = string.Empty;
        }

        private void TryAddTextToParsedContent(List<MastoContent> ParsedContent, string ContentToAdd, bool IsParagraph = true)
        {
            ContentToAdd = ContentToAdd.Replace(">", "");

            if (ContentToAdd != string.Empty) ParsedContent.Add(new MastoText(WebUtility.HtmlDecode(ContentToAdd)));
        }

        private bool CheckIfTag(string ParsedTag, string ExpectedTag)
        {
            return ParsedTag == ExpectedTag;
        }

        private bool LoopConditionIsTrue(string Tag, string ParsedTag)
        {
            bool willLoopContinue;
            if (Tag == string.Empty)
                willLoopContinue = _charQueue.Count > 0;
            else
                willLoopContinue = ParsedTag != $"/{Tag}";

            return willLoopContinue;
        }

        public List<MastoContent> ParseContent(string HtmlContent)
        {
            _charQueue = new Queue<char>(HtmlContent);

            // At first, you'll start with no tag
            List<MastoContent> parsedContent = ParseLoop(string.Empty);

            return parsedContent;
        }

        private (bool hasTextToParse, bool hasTagToParse, string text, string tag) HandleText(char Character)
        {
            bool hasTextToParse = false;
            bool hasTagToParse = false;
            string text = null;
            string tag = null;

            if (Character == ParserConstants.TAG_START_CHARACTER)
            {
                if (_parseBuffer.Length > 0)
                {
                    text = _parseBuffer.ToString();
                    _parseBuffer.Clear();
                    hasTextToParse = true;
                }

                hasTagToParse = true;
            }

            if (hasTagToParse)
                tag = ParseTag();
            else
                _parseBuffer.Append(Character);

            return (hasTextToParse, hasTagToParse, text, tag);
        }

        private string ParseTag()
        {
            // '<' has been removed from queue already.
            // so you just need to add all characters before a
            // (space).

            StringBuilder parsedTagBuffer = new StringBuilder();
            while (_charQueue.Peek() != _SPACE_CHAR && _charQueue.Peek() != '>')
            {
                char thisChar = _charQueue.Dequeue();
                parsedTagBuffer.Append(thisChar);
            }

            // Code for skipping over span attributes
            if (parsedTagBuffer.ToString().Contains(ParserConstants.SPAN_TAG))
            {
                while (_charQueue.Peek() != '>')
                    _charQueue.Dequeue();
            }

            return parsedTagBuffer.ToString();
        }

        private Dictionary<string, string> FindAttributes()
        {
            Dictionary<string, string> tagAttributes = new Dictionary<string, string>();
            StringBuilder attributeBuffer = new StringBuilder();
            string currentAttribute = "";
            bool hasTagBeenClosed = false;
            bool isInAttributeValue = false;

            while (!hasTagBeenClosed)
            {
                char character = _charQueue.Dequeue();
                if (character == _SPACE_CHAR && isInAttributeValue == false)
                {
                    if (currentAttribute != string.Empty)
                    {
                        tagAttributes[currentAttribute] = attributeBuffer.ToString();
                        currentAttribute = string.Empty;
                        attributeBuffer.Clear();
                    }
                }
                else if (character == '>')
                {
                    // First tag has been closed
                    hasTagBeenClosed = true;

                    if (currentAttribute != string.Empty)
                    {
                        tagAttributes[currentAttribute] = attributeBuffer.ToString();
                        currentAttribute = string.Empty;
                        attributeBuffer.Clear();
                    }
                }
                else
                {
                    // Fill in attribute name
                    if (currentAttribute == string.Empty)
                    {
                        if (character == '=')
                        {
                            currentAttribute = attributeBuffer.ToString();
                            attributeBuffer.Clear();
                        }
                        else
                        {
                            attributeBuffer.Append(character);
                        }
                    }
                    else // currentAttribute has been filled, handle attribute values now
                    {
                        if (character == '"')
                            isInAttributeValue = !isInAttributeValue;
                        else
                            attributeBuffer.Append(character);
                    }
                }
            }

            return tagAttributes;
        }

        public MastoContent HandleLinkTag()
        {
            MastoContent contentToReturn = null;

            Dictionary<string, string> tagAttributes = FindAttributes();

            // Now try to take action depending on the result of trying to find the "class" attribute
            bool hasClassAttribute = tagAttributes.ContainsKey(ParserConstants.CLASS_ATTRIBUTE);
            bool isUniqueLink = false;

            string classAttributeValue = string.Empty;

            if (hasClassAttribute)
            {
                classAttributeValue = tagAttributes[ParserConstants.CLASS_ATTRIBUTE];
                if (classAttributeValue != string.Empty) isUniqueLink = true;
            }

            if (isUniqueLink)
            {
                // handle a mention/hashtag.
                switch (classAttributeValue)
                {
                    case ParserConstants.HASHTAG_CLASS:
                        contentToReturn = ParseUniqueLink('#');
                        break;
                    case ParserConstants.MENTION_CLASS:
                        contentToReturn = ParseUniqueLink('@');
                        break;
                    case ParserConstants.PLAIN_HASHTAG_CLASS:
                        contentToReturn = PlainHashtagParse();
                        break;
                }
            }
            else
            {
                // Do regular link stuff
                contentToReturn = new MastoContent(tagAttributes[ParserConstants.LINK_HREF], MastoContentType.Link);
                SkipToLinkTagEnd();
            }

            return contentToReturn;
        }

        private MastoContent PlainHashtagParse()
        {
            StringBuilder plainHashtagBuffer = new StringBuilder();

            while (true)
            {
                char charFound = _charQueue.Dequeue();
                if (charFound == '<')
                    break;
                if (charFound != '#') plainHashtagBuffer.Append(charFound);
            }

            MastoContent contentToReturn = new MastoContent(plainHashtagBuffer.ToString(), MastoContentType.Hashtag);

            return contentToReturn;
        }

        private MastoContent ParseUniqueLink(char UniqueChar)
        {
            MastoContent contentToReturn = null;
            bool wasUniqueCharFound = false;
            bool linkTagEndReached = false;
            bool spanTagReached = false;
            bool isInSpanTagContent = false;

            StringBuilder uniqueLinkBuffer = new StringBuilder();

            while (!linkTagEndReached)
            {
                char charFound = _charQueue.Dequeue();
                if (wasUniqueCharFound == false)
                {
                    if (charFound == UniqueChar) wasUniqueCharFound = true;
                }
                else if (!spanTagReached)
                {
                    if (charFound == '<') spanTagReached = true;
                }
                else if (isInSpanTagContent)
                {
                    if (charFound == '<')
                    {
                        // Save buffer content
                        // clear buffer
                        // dequeue to link tag end
                        // set isLinkTagReached to true
                        string uniqueLinkContent = WebUtility.HtmlDecode(uniqueLinkBuffer.ToString());
                        MastoContentType contentType = DetermineContentTypeFromChar(UniqueChar);
                        contentToReturn = new MastoContent(uniqueLinkContent, contentType);
                        uniqueLinkBuffer.Clear();
                        SkipToLinkTagEnd();
                        linkTagEndReached = true;
                    }
                    else
                    {
                        uniqueLinkBuffer.Append(charFound);
                    }
                }
                else if (spanTagReached)
                {
                    if (charFound == '>') isInSpanTagContent = true;
                }
            }

            return contentToReturn;
        }

        private void SkipToLinkTagEnd()
        {
            StringBuilder stringBuffer = new StringBuilder();

            while (!stringBuffer.ToString().Contains($"</{ParserConstants.LINK_TAG}>")) stringBuffer.Append(_charQueue.Dequeue());
        }

        private MastoContentType DetermineContentTypeFromChar(char UniqueChar)
        {
            if (UniqueChar == '#')
                return MastoContentType.Hashtag;

            return MastoContentType.Mention;
        }
    }
}