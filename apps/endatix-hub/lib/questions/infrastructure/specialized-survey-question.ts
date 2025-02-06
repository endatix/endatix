import {
  ComponentCollection,
  ICustomQuestionTypeConfiguration,
} from "survey-core";
import { SurveyCreator } from "survey-creator-react";

/**
 * Abstract base class for creating custom question types in SurveyJS.
 * Provides a standardized way to define and register specialized survey questions.
 */
export abstract class SpecializedSurveyQuestion {
  /**
   * Gets the configuration object that defines this custom question type.
   * The configuration specifies properties like name, title, icon, and behavior.
   * Must be implemented by concrete question classes.
   */
  abstract get customQuestionConfig(): ICustomQuestionTypeConfiguration;

  /**
   * Customizes the SurveyCreator toolbox and other editor-specific settings for this question type.
   * Must be implemented as a static method by concrete question classes.
   * @param creator - The SurveyCreator instance to customize
   */
  static customizeEditor(creator: SurveyCreator): void {
    console.error("customizeEditor not implemented", creator);

    throw new Error("customizeEditor method not implemented.");
  }
}

/**
 * Registers a specialized question type with the SurveyJS ComponentCollection.
 * @param questionClass - The specialized question class to register
 */
export function registerSpecializedQuestion(
  questionClass: typeof SpecializedSurveyQuestion,
) {
  const instance =
    new (questionClass as unknown as new () => SpecializedSurveyQuestion)();
  ComponentCollection.Instance.add(instance.customQuestionConfig);
}
