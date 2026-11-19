import { expect, type Page } from '@playwright/test';

// The tests' API runs on the stand-in model (playwright.config.ts, ADR-019 decision 10). Its writer
// sends this one sheet for every character: StandInChatClient.Sheet, word for word.
export const standInSheet =
  '## A stand-in character\n\nThis sheet came from the stand-in model, which writes the same text for every character. No model was called.';

// The stand-in records only a choice the scene names, so it leaves the place and the role to the GM,
// and the interview asks for both, in the roll's order. These are the GM's answers.
export const answers = [
  { field: 'setting', text: 'an inn in a major city' },
  { field: 'occupation', text: 'fence' },
];

export const sceneBox = (page: Page) => page.getByRole('textbox', { name: 'DESCRIBE YOUR SCENE' });
export const confirmation = (page: Page) => page.getByRole('region', { name: 'Who is being made' });

// From the forge page's first step to the confirmation, through the API: the scene, then an answer
// to each question. The box for each question is named by its field, so each step waits for the one
// it answers.
export async function toConfirmation(page: Page, scene: string) {
  await sceneBox(page).fill(scene);
  await page.getByRole('button', { name: 'SEND THE SCENE' }).click();
  for (const { field, text } of answers) {
    await page.getByRole('textbox', { name: new RegExp(`^${field} `) }).fill(text);
    await page.getByRole('button', { name: 'ANSWER' }).click();
  }
  await expect(confirmation(page)).toBeVisible();
}
