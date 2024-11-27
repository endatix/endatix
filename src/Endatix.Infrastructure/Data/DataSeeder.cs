using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Class that inserts preprogrammed sample data into the database. Designed for creating sample data. Can evolve to support testing case or SDK templates
/// </summary>
public class DataSeeder(ILogger _logger, IIdGenerator<long> _idGenerator, AppDbContext dbContext)
{
    /// <summary>
    /// Main method to load data into the database
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="logger"></param>
    public void PopulateTestData()
    {
        var hasFormsPopulated = dbContext.Forms.Any();
        if (hasFormsPopulated)
        {
            _logger.LogInformation("Found Form records in the database table. Skipping seeding data...");
            return;
        }

        var stopWatch = Stopwatch.StartNew();
        _logger.LogInformation("Seeding data initiated");

        #region Populate Forms
        Form sampleForm1 = new("Product Feedback Survey", formDefinitionJson: PRODUCT_FEEDBACK_SURVEY_JSON)
        {
            Id = _idGenerator.CreateId()
        };
        sampleForm1.ActiveDefinition.Id = _idGenerator.CreateId();

        Form sampleForm2 = new("Booking Form", formDefinitionJson: BOOKING_FORM_JSON)
        {
            Id = _idGenerator.CreateId()
        };

        Form sampleForm3 = new("Contact Us Form", formDefinitionJson: CONTACT_US_FORM_JSON)
        {
            Id = 1266039221823471616
        };
        sampleForm3.ActiveDefinition.Id = _idGenerator.CreateId();

        dbContext.Forms.AddRange(sampleForm1, sampleForm2, sampleForm3);
        dbContext.SaveChanges();

        _logger.LogInformation("     >> Sample forms added");

        #endregion

        #region Populate Definitions
        FormDefinition formDefinition2 = new(jsonData: PRODUCT_FEEDBACK_SURVEY_JSON)
        {
            Form = sampleForm1,
            Id = _idGenerator.CreateId()
        };

        FormDefinition formDefinition3 = new(jsonData: PRODUCT_FEEDBACK_SURVEY_JSON)
        {
            Form = sampleForm1,
            Id = _idGenerator.CreateId()
        };
        dbContext.FormDefinitions.AddRange(formDefinition2, formDefinition3);
        dbContext.SaveChanges();

        _logger.LogInformation("     >> Additional form definitions added");

        #endregion

        #region Populate Submissions
        var submission1 = new Submission("{\"missing-feature\":\"Replicator\"}", formDefinitionId: sampleForm1.ActiveDefinition.Id)
        {
            Id = _idGenerator.CreateId()
        };
        var submission2 = new Submission("{\"missing-feature\":\"Rakia Thing\"}", formDefinitionId: formDefinition2.Id)
        {
            Id = _idGenerator.CreateId()
        };
        var submission3 = new Submission("{\"missing-feature\":\"Auto-repair\"}", formDefinitionId: formDefinition2.Id)
        {
            Id = _idGenerator.CreateId()
        };
        var submission4 = new Submission("{\"missing-feature\":\"Chin\"}", formDefinitionId: formDefinition3.Id)
        {
            Id = _idGenerator.CreateId()
        };
        var submission5 = new Submission("{\"missing-feature\":\"Espresso Machine\"}", formDefinitionId: sampleForm1.ActiveDefinition.Id)
        {
            Id = _idGenerator.CreateId()
        };

        dbContext.Submissions.AddRange(submission1, submission2, submission3, submission4, submission5);
        dbContext.SaveChanges();

        _logger.LogInformation("     >> Form submissions added");
        #endregion

        stopWatch.Stop();
        _logger.LogInformation("Seeding data completed. Time taken: {time} ms", stopWatch.ElapsedMilliseconds);
    }

    private const string PRODUCT_FEEDBACK_SURVEY_JSON = "{\"title\":\"Product Feedback Survey\",\"description\":\"Your opinion matters to us!\",\"logo\":\"https://api.surveyjs.io/private/Surveys/files?name=df89f942-7e47-48e0-9fc0-b64608584b4c\",\"logoFit\":\"cover\",\"logoPosition\":\"right\",\"logoHeight\":\"100px\",\"elements\":[{\"type\":\"radiogroup\",\"name\":\"discovery-source\",\"title\":\"How did you first hear about us?\",\"choices\":[\"Search engine (Google, Bing, etc.)\",\"Online newsletter\",\"Blog post\",\"Word of mouth\",\"Social media\"],\"showOtherItem\":true,\"otherPlaceholder\":\"Please specify...\",\"otherText\":\"Other\"},{\"type\":\"radiogroup\",\"name\":\"social-media-platform\",\"visibleIf\":\"{discovery-source} = 'Social media'\",\"title\":\"Which platform?\",\"choices\":[\"YouTube\",\"Facebook\",\"Instagram\",\"TikTok\",\"LinkedIn\"],\"showOtherItem\":true,\"otherPlaceholder\":\"Please specify...\",\"otherText\":\"Other\"},{\"type\":\"matrix\",\"name\":\"quality\",\"title\":\"To what extent do you agree with the following statements?\",\"columns\":[{\"value\":1,\"text\":\"Strongly disagree\"},{\"value\":2,\"text\":\"Disagree\"},{\"value\":3,\"text\":\"Undecided\"},{\"value\":4,\"text\":\"Agree\"},{\"value\":5,\"text\":\"Strongly agree\"}],\"rows\":[{\"text\":\"The product meets my needs\",\"value\":\"needs-are-met\"},{\"text\":\"Overall, I am satisfied with the product\",\"value\":\"satisfaction\"},{\"text\":\"Some product features require improvement\",\"value\":\"improvements-required\"}],\"columnMinWidth\":\"40px\",\"rowTitleWidth\":\"300px\"},{\"type\":\"rating\",\"name\":\"buying-experience\",\"title\":\"How would you rate the buying experience?\",\"minRateDescription\":\"Hated it!\",\"maxRateDescription\":\"Loved it!\"},{\"type\":\"comment\",\"name\":\"low-score-reason\",\"visibleIf\":\"{buying-experience} <= 3\",\"titleLocation\":\"hidden\",\"hideNumber\":true,\"placeholder\":\"What's the main reason for your score?\",\"maxLength\":500},{\"type\":\"boolean\",\"name\":\"have-used-similar-products\",\"title\":\"Have you used similar products before?\"},{\"type\":\"text\",\"name\":\"similar-products\",\"visibleIf\":\"{have-used-similar-products} = true\",\"titleLocation\":\"hidden\",\"hideNumber\":true,\"placeholder\":\"Please specify the similar products...\"},{\"type\":\"ranking\",\"name\":\"product-aspects-ranked\",\"title\":\"These are some important aspects of the product. Rank them in terms of your priority.\",\"description\":\"From the highest (the most important) to the lowest (the least important).\",\"choices\":[\"Technical support\",\"Price\",\"Delivery option\",\"Quality\",\"Ease of use\",\"Product warranties\"]},{\"type\":\"text\",\"name\":\"missing-feature\",\"title\":\"What's the ONE thing our product is missing?\"},{\"type\":\"dropdown\",\"name\":\"price-accuracy\",\"title\":\"Do you feel our product is worth the cost?\",\"choices\":[{\"value\":5,\"text\":\"Definitely\"},{\"value\":4,\"text\":\"Probably\"},{\"value\":3,\"text\":\"Maybe\"},{\"value\":2,\"text\":\"Probably not\"},{\"value\":1,\"text\":\"Definitely not\"}],\"allowClear\":false},{\"type\":\"boolean\",\"name\":\"have-additional-thoughts\",\"title\":\"Is there anything you'd like to add?\"},{\"type\":\"comment\",\"name\":\"additional-thoughts\",\"visibleIf\":\"{have-additional-thoughts} = true\",\"titleLocation\":\"hidden\",\"placeholder\":\"Please share your thoughts...\"}],\"showProgressBar\":\"top\",\"progressBarType\":\"questions\",\"widthMode\":\"static\",\"width\":\"864px\"}";

    private const string BOOKING_FORM_JSON = "{\"title\":\"HOTEL BY THE SEA\",\"description\":\"1901 Thornridge Cir. Shiloh, Hawaii 81063 +1 (808) 555-0111\",\"logo\":\"https://api.surveyjs.io/private/Surveys/files?name=cd99ec15-4054-4e75-8e42-9edff605a5d4\",\"logoWidth\":\"auto\",\"logoHeight\":\"80\",\"completedHtml\":\"<div style=\\\"max-width:688px;text-align:center;margin: 16px auto;\\\">\\n\\n<div style=\\\"padding:0 24px;\\\">\\n<h4>Thank you for choosing us.</h4>\\n<br>\\n<p>Dear {firstname-for-complete-page}, we're thrilled to have you on board and excited to be a part of your upcoming journey. Your reservation is confirmed, and we can't wait to make your travel experience exceptional.</p>\\n</div>\\n\\n</div>\\n\",\"pages\":[{\"name\":\"page1\",\"elements\":[{\"type\":\"text\",\"name\":\"check-in-date\",\"width\":\"37%\",\"minWidth\":\"256px\",\"titleLocation\":\"hidden\",\"description\":\"Check-in\",\"descriptionLocation\":\"underInput\",\"defaultValueExpression\":\"today()\",\"validators\":[{\"type\":\"expression\",\"text\":\"Check-in date cannot precede today's date.\",\"expression\":\"{check-in-date} >= today()\"}],\"inputType\":\"date\",\"placeholder\":\"Check-in\"},{\"type\":\"text\",\"name\":\"check-out-date\",\"width\":\"37%\",\"minWidth\":\"256px\",\"startWithNewLine\":false,\"titleLocation\":\"hidden\",\"description\":\"Check-out\",\"descriptionLocation\":\"underInput\",\"defaultValueExpression\":\"today(1)\",\"validators\":[{\"type\":\"expression\",\"text\":\"Invalid date range: check-out date cannot precede check-in date.\",\"expression\":\"getDate({check-out-date}) >= getDate({check-in-date})\"}],\"inputType\":\"date\",\"placeholder\":\"Check-out\"},{\"type\":\"dropdown\",\"name\":\"number-of-guests\",\"width\":\"26%\",\"minWidth\":\"192px\",\"startWithNewLine\":false,\"titleLocation\":\"hidden\",\"choices\":[1,2,3,4,5,6,7,8,9,{\"value\":\"10\",\"text\":\"10+\"}],\"placeholder\":\"# of guests\",\"allowClear\":false},{\"type\":\"dropdown\",\"name\":\"room-type\",\"useDisplayValuesInDynamicTexts\":false,\"width\":\"74%\",\"minWidth\":\"256px\",\"titleLocation\":\"hidden\",\"choices\":[{\"value\":\"queen\",\"text\":\"Queen Room\"},{\"value\":\"king\",\"text\":\"King Room\"},{\"value\":\"deluxe-king\",\"text\":\"Deluxe King Room\"},{\"value\":\"superior-king\",\"text\":\"Superior King Room\"}],\"placeholder\":\"Room type\",\"allowClear\":false},{\"type\":\"checkbox\",\"name\":\"non-smoking\",\"width\":\"26%\",\"minWidth\":\"192px\",\"startWithNewLine\":false,\"titleLocation\":\"hidden\",\"choices\":[{\"value\":\"true\",\"text\":\"Non-smoking\"}]},{\"type\":\"image\",\"name\":\"king-room-image\",\"visibleIf\":\"{room-type} = 'king'\",\"width\":\"37%\",\"minWidth\":\"192px\",\"imageLink\":\"https://api.surveyjs.io/private/Surveys/files?name=31ba1c67-201e-458e-b30b-86b45ba25f40\",\"imageFit\":\"cover\",\"imageHeight\":\"224\",\"imageWidth\":\"1000\"},{\"type\":\"html\",\"name\":\"king-room-description\",\"visibleIf\":\"{room-type} = 'king'\",\"width\":\"63%\",\"minWidth\":\"256px\",\"startWithNewLine\":false,\"html\":\"<h4 style=\\\"padding-top:16px\\\">King Room</h4>\\n<p style=\\\"padding-top:8px;font-size:14px;\\\">\\nOur King Room offers spacious luxury with a king-sized bed for a great night's sleep. Stay connected with complimentary Wi-Fi, refresh in the private bathroom, and enjoy in-room entertainment with a flat-screen TV. Keep your favorite beverages cool in the mini-fridge, and start your day right with a coffee from the in-room coffee maker. The ideal retreat for your travels.\\n</p>\\n\\n\"},{\"type\":\"image\",\"name\":\"deluxe-king-room-image\",\"visibleIf\":\"{room-type} = 'deluxe-king'\",\"width\":\"37%\",\"minWidth\":\"192px\",\"imageLink\":\"https://api.surveyjs.io/private/Surveys/files?name=4fc633b5-0ac3-48f5-9728-284446e72adf\",\"imageFit\":\"cover\",\"imageHeight\":\"224\",\"imageWidth\":\"1000\"},{\"type\":\"html\",\"name\":\"deluxe-king-room-description\",\"visibleIf\":\"{room-type} = 'deluxe-king'\",\"width\":\"63%\",\"minWidth\":\"256px\",\"startWithNewLine\":false,\"html\":\"<h4 style=\\\"padding-top:16px\\\">Deluxe King Room</h4>\\n<p style=\\\"padding-top:8px;font-size:14px;\\\">\\nElevate your stay in our Deluxe King Room. Experience ultimate comfort on a luxurious king-sized bed. Enjoy the convenience of complimentary Wi-Fi, a private bathroom, and entertainment on a flat-screen TV. Stay refreshed with a well-stocked mini-fridge and coffee maker. With added space and upscale amenities, this room offers a touch of luxury for a truly special stay.\\n</p>\\n\\n\"},{\"type\":\"image\",\"name\":\"queen-room-image\",\"visibleIf\":\"{room-type} = 'queen'\",\"width\":\"37%\",\"minWidth\":\"192px\",\"imageLink\":\"https://api.surveyjs.io/private/Surveys/files?name=2e2bc916-6f2e-47ff-b321-74b34118a748\",\"imageFit\":\"cover\",\"imageHeight\":\"224\",\"imageWidth\":\"1000\"},{\"type\":\"html\",\"name\":\"queen-room-description\",\"visibleIf\":\"{room-type} = 'queen'\",\"width\":\"63%\",\"minWidth\":\"256px\",\"startWithNewLine\":false,\"html\":\"<h4 style=\\\"padding-top:16px\\\">Queen Room</h4>\\n<p style=\\\"padding-top:8px;font-size:14px;\\\">\\nExperience comfort and convenience in our Queen Room. Unwind on a cozy queen-sized bed, stay connected with complimentary Wi-Fi, and enjoy the convenience of a private bathroom. For your entertainment, there's a flat-screen TV. A mini-fridge and coffee maker are at your disposal for added convenience. Your perfect choice for a relaxing stay.\\n</p>\\n\\n\"},{\"type\":\"image\",\"name\":\"superior-king-room-image\",\"visibleIf\":\"{room-type} = 'superior-king'\",\"width\":\"37%\",\"minWidth\":\"192px\",\"imageLink\":\"https://api.surveyjs.io/private/Surveys/files?name=e16770dd-818c-4847-8b7f-19ee527420c1\",\"imageFit\":\"cover\",\"imageHeight\":\"224\",\"imageWidth\":\"1000\"},{\"type\":\"html\",\"name\":\"superior-king-room-description\",\"visibleIf\":\"{room-type} = 'superior-king'\",\"width\":\"63%\",\"minWidth\":\"256px\",\"startWithNewLine\":false,\"html\":\"<h4 style=\\\"padding-top:16px\\\">Superior King Room</h4>\\n<p style=\\\"padding-top:8px;font-size:14px;\\\">\\nIndulge in the epitome of luxury in our Superior King Room. Experience ample space and opulence with a king-sized bed. Complimentary Wi-Fi keeps you connected, while the private bathroom and flat-screen TV provide comfort and entertainment. Enjoy the convenience of a well-equipped mini-fridge and coffee maker. This room is the top choice for a superior and memorable stay.\\n</p>\\n\\n\"},{\"type\":\"dropdown\",\"name\":\"number-of-rooms\",\"width\":\"37%\",\"minWidth\":\"192px\",\"titleLocation\":\"hidden\",\"choices\":[1,2,3,4,5],\"placeholder\":\"# of rooms\",\"allowClear\":false},{\"type\":\"checkbox\",\"name\":\"with-pets\",\"width\":\"63%\",\"minWidth\":\"256px\",\"startWithNewLine\":false,\"titleLocation\":\"hidden\",\"choices\":[{\"value\":\"true\",\"text\":\"I am traveling with pets\"}]},{\"type\":\"tagbox\",\"name\":\"extras\",\"width\":\"100%\",\"minWidth\":\"256px\",\"titleLocation\":\"hidden\",\"choices\":[\"Breakfast\",\"Fitness\",\"Parking\",\"Swimming pool\",\"Restaurant\",\"Spa\"],\"placeholder\":\"Extras\"},{\"type\":\"comment\",\"name\":\"notes\",\"width\":\"100%\",\"minWidth\":\"256px\",\"titleLocation\":\"hidden\",\"placeholder\":\"Notes...\",\"autoGrow\":true,\"allowResize\":false}]},{\"name\":\"page2\",\"elements\":[{\"type\":\"text\",\"name\":\"last-name\",\"width\":\"64%\",\"minWidth\":\"192px\",\"titleLocation\":\"hidden\",\"description\":\"Must match the passport exactly\",\"descriptionLocation\":\"underInput\",\"placeholder\":\"Last name\"},{\"type\":\"text\",\"name\":\"first-name\",\"width\":\"36%\",\"minWidth\":\"256px\",\"startWithNewLine\":false,\"titleLocation\":\"hidden\",\"placeholder\":\"First name\"},{\"type\":\"text\",\"name\":\"address-line-1\",\"width\":\"100%\",\"minWidth\":\"256px\",\"titleLocation\":\"hidden\",\"descriptionLocation\":\"underInput\",\"placeholder\":\"Address line 1\"},{\"type\":\"text\",\"name\":\"address-line-2\",\"width\":\"100%\",\"minWidth\":\"256px\",\"titleLocation\":\"hidden\",\"placeholder\":\"Address line 2\"},{\"type\":\"text\",\"name\":\"city\",\"width\":\"36%\",\"minWidth\":\"256px\",\"titleLocation\":\"hidden\",\"placeholder\":\"City\"},{\"type\":\"text\",\"name\":\"zip\",\"width\":\"28%\",\"minWidth\":\"192px\",\"startWithNewLine\":false,\"titleLocation\":\"hidden\",\"placeholder\":\"Zip code\"},{\"type\":\"text\",\"name\":\"state\",\"width\":\"36%\",\"minWidth\":\"256px\",\"startWithNewLine\":false,\"titleLocation\":\"hidden\",\"placeholder\":\"State\"},{\"type\":\"dropdown\",\"name\":\"country\",\"width\":\"36%\",\"minWidth\":\"256px\",\"titleLocation\":\"hidden\",\"choicesByUrl\":{\"url\":\"https://surveyjs.io/api/CountriesExample\"},\"placeholder\":\"Country\",\"allowClear\":false},{\"type\":\"text\",\"name\":\"phone\",\"width\":\"64%\",\"minWidth\":\"192px\",\"startWithNewLine\":false,\"titleLocation\":\"hidden\",\"description\":\"Example: +1 (555) 777-55-22\",\"descriptionLocation\":\"underInput\",\"placeholder\":\"Phone\"}]}],\"calculatedValues\":[{\"name\":\"firstname-for-complete-page\",\"expression\":\"iif({first-name} notempty, {first-name}, guests)\"}],\"showPrevButton\":false,\"showQuestionNumbers\":\"off\",\"questionErrorLocation\":\"bottom\",\"pagePrevText\":\"Booking Details\",\"pageNextText\":\"Traveler Info ➝\",\"completeText\":\"Book Now\",\"widthMode\":\"static\",\"width\":\"904\",\"fitToContainer\":true}";

    private const string CONTACT_US_FORM_JSON = "{\"title\":\"Contact Us\",\"description\":\"Please fill out the form below to get in touch with us.\",\"pages\":[{\"name\":\"contactForm\",\"elements\":[{\"type\":\"panel\",\"name\":\"contactPanel\",\"elements\":[{\"type\":\"text\",\"name\":\"name\",\"title\":\"Name\",\"isRequired\":true,\"placeHolder\":\"Your Name\",\"width\":\"50%\"},{\"type\":\"text\",\"name\":\"email\",\"title\":\"Email\",\"inputType\":\"email\",\"isRequired\":true,\"placeHolder\":\"Your Email\",\"startWithNewLine\":false,\"width\":\"50%\"},{\"type\":\"text\",\"name\":\"phone\",\"title\":\"Phone Number\",\"inputType\":\"tel\",\"isRequired\":false,\"placeHolder\":\"Your Phone Number\",\"width\":\"50%\"},{\"type\":\"text\",\"name\":\"company\",\"title\":\"Company Name\",\"isRequired\":false,\"placeHolder\":\"Your Company Name\",\"startWithNewLine\":false,\"width\":\"50%\"},{\"type\":\"ranking\",\"name\":\"importanceRanking\",\"title\":\"Please rank the following according to the importance to you:\",\"isRequired\":true,\"choices\":[\"Data Security and Privacy Concerns\",\"Integration with Existing Systems\",\"Business User Experience\",\"Automation of Collected Data Processing\",\"Customization and Flexibility\",\"Other (please specify below)\"]},{\"type\":\"comment\",\"name\":\"message\",\"title\":\"Share about your project and ask any questions you have\",\"isRequired\":true,\"placeHolder\":\"Your Message\"}]}]}],\"showQuestionNumbers\":\"off\",\"completeText\":\"Send\",\"completedHtml\":\"<p>Thank you for your message. We will get back to you shortly.</p>\"}";
}
