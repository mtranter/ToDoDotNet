# Todo

A todo-tracking domain: people keep named lists, and each list holds items that can be completed, reopened, and given deadlines. The domain exists to be *read* as much as run — it is the worked example in a repository teaching .NET to developers arriving from Java/Kotlin and Spring Boot.

## Language

**Todo List**:
A named collection of Todo Items. The only thing in this domain that exists independently — every other concept is reached through one.
_Avoid_: List (collides with `List<T>`), Board, Project, Category

**Todo Item**:
A single piece of work recorded inside exactly one Todo List. Never exists on its own, and is always identified relative to its list.
_Avoid_: Task (collides with `System.Threading.Tasks.Task`), Todo, Entry, Card, Note

**Title**:
The short human-readable label carried by both a Todo List and a Todo Item. Non-empty, and bounded in length.
_Avoid_: Name, Label, Summary, Description

**Due Date**:
The optional moment by which a Todo Item is meant to be complete. May only be set to a future moment; once assigned, the passage of time does not invalidate it.
_Avoid_: Deadline, Due, Expiry, Target Date

**Overdue**:
The condition of an incomplete Todo Item whose Due Date has passed. Derived by comparison, never stored, and never a reason to reject an unrelated change.
_Avoid_: Late, Expired, Missed

## Operations

**Complete**:
To mark a Todo Item as finished. Only meaningful on an item that is not already complete.
_Avoid_: Done, Finish, Close, Check Off, Tick

**Reopen**:
To return a completed Todo Item to its unfinished state. Only meaningful on an item that is currently complete.
_Avoid_: Uncomplete, Undo, Reset, Restore

**Rename**:
To replace the Title of a Todo List or a Todo Item.
_Avoid_: Edit, Update, Retitle

**Remove**:
To take a Todo Item out of its Todo List, ending its existence. Always expressed against the list that holds it, never against the item alone.
_Avoid_: Delete, Archive, Discard

**Delete**:
To end the existence of a Todo List, and with it every Todo Item the list holds.
_Avoid_: Remove, Archive, Purge
