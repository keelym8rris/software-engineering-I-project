// Blazor calls this after rendering an updated chat message list.
export function scrollToLatest(element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
}
