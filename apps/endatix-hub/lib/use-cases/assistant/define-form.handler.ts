export enum DefineFormCommand {
    formModelUpdate = 'formModelUpdate',
    threadUpdate = 'threadUpdate',
    fullStateUpdate = 'fullStateUpdate'
}

export const defineFormHandler = (command: DefineFormCommand) => {
    switch (command) {
        case DefineFormCommand.formModelUpdate:
            break;
        case DefineFormCommand.threadUpdate:
            break;
        case DefineFormCommand.fullStateUpdate:
            break;
    }
}