@Tier:L3
Feature: Hold-to-talk dictation
  As a FreeFlow user
  I hold a shortcut to dictate, release to transcribe, clean up, and paste.

  Scenario: Hold shortcut transcribes and pastes cleaned text
    Given the focused app is "Notepad"
    And the clipboard currently contains "PREVIOUS"
    And the microphone will capture fixture audio
    And the transcription provider will return "um hello world"
    And the post-processor will return "Hello world."
    When the user holds the dictation shortcut for 1200 ms
    And the user releases the dictation shortcut
    Then the pipeline ends in state "Idle"
    And the pasted text is "Hello world."
    And the clipboard is restored to "PREVIOUS"
